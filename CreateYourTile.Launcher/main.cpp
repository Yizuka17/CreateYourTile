#include <windows.h>
#include <appmodel.h>
#include <shlobj.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <wincrypt.h>
#include <wincodec.h>
#include <cstdint>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <string>
#include <vector>

namespace
{
    std::wstring Utf8ToWide(const std::string& text)
    {
        if (text.empty())
        {
            return std::wstring();
        }
        int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(),
            static_cast<int>(text.size()), nullptr, 0);
        if (length <= 0)
        {
            return std::wstring();
        }
        std::wstring result(static_cast<size_t>(length), L'\0');
        MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text.data(),
            static_cast<int>(text.size()), &result[0], length);
        return result;
    }

    std::string WideToUtf8(const std::wstring& text)
    {
        if (text.empty())
        {
            return std::string();
        }
        int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(),
            static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
        if (length <= 0)
        {
            return std::string();
        }
        std::string result(static_cast<size_t>(length), '\0');
        WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(),
            static_cast<int>(text.size()), &result[0], length, nullptr, nullptr);
        return result;
    }

    std::string EncodeBase64(const std::wstring& value)
    {
        std::string bytes = WideToUtf8(value);
        if (bytes.empty())
        {
            return std::string();
        }

        DWORD outputLength = 0;
        if (!CryptBinaryToStringA(reinterpret_cast<const BYTE*>(bytes.data()),
            static_cast<DWORD>(bytes.size()), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF,
            nullptr, &outputLength))
        {
            return std::string();
        }

        std::string output(outputLength, '\0');
        if (!CryptBinaryToStringA(reinterpret_cast<const BYTE*>(bytes.data()),
            static_cast<DWORD>(bytes.size()), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF,
            &output[0], &outputLength))
        {
            return std::string();
        }
        if (!output.empty() && output.back() == '\0')
        {
            output.pop_back();
        }
        return output;
    }

    std::wstring DecodeBase64(const std::string& encoded)
    {
        DWORD byteCount = 0;
        if (!CryptStringToBinaryA(encoded.c_str(), static_cast<DWORD>(encoded.size()),
            CRYPT_STRING_BASE64, nullptr, &byteCount, nullptr, nullptr))
        {
            return std::wstring();
        }
        std::vector<BYTE> bytes(byteCount);
        if (!CryptStringToBinaryA(encoded.c_str(), static_cast<DWORD>(encoded.size()),
            CRYPT_STRING_BASE64, bytes.data(), &byteCount, nullptr, nullptr))
        {
            return std::wstring();
        }
        return Utf8ToWide(std::string(reinterpret_cast<const char*>(bytes.data()), byteCount));
    }

    std::wstring GetRequestPath()
    {
        UINT32 familyLength = 0;
        if (GetCurrentPackageFamilyName(&familyLength, nullptr) != ERROR_INSUFFICIENT_BUFFER)
        {
            return std::wstring();
        }
        std::vector<wchar_t> family(familyLength);
        if (GetCurrentPackageFamilyName(&familyLength, family.data()) != ERROR_SUCCESS)
        {
            return std::wstring();
        }

        PWSTR localAppData = nullptr;
        KNOWN_FOLDER_FLAG noPackageRedirect = static_cast<KNOWN_FOLDER_FLAG>(0x00010000);
        if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, noPackageRedirect, nullptr, &localAppData)))
        {
            return std::wstring();
        }
        std::wstring path(localAppData);
        CoTaskMemFree(localAppData);
        path += L"\\Packages\\";
        path += family.data();
        path += L"\\LocalState\\launch-request.txt";
        return path;
    }

    bool ReadLine(std::ifstream& stream, std::string& value)
    {
        if (!std::getline(stream, value))
        {
            return false;
        }
        if (!value.empty() && value.back() == '\r')
        {
            value.pop_back();
        }
        return true;
    }

    std::wstring GetParentDirectory(const std::wstring& path)
    {
        size_t separator = path.find_last_of(L"\\/");
        return separator == std::wstring::npos ? std::wstring() : path.substr(0, separator);
    }

    bool IsSafeCatalogFileName(const std::wstring& fileName)
    {
        const std::wstring prefix = L"app-catalog-";
        const std::wstring suffix = L".txt";
        if (fileName.size() <= prefix.size() + suffix.size() ||
            fileName.compare(0, prefix.size(), prefix) != 0 ||
            fileName.compare(fileName.size() - suffix.size(), suffix.size(), suffix) != 0)
        {
            return false;
        }
        for (wchar_t character : fileName)
        {
            if (!(character >= L'a' && character <= L'z') &&
                !(character >= L'A' && character <= L'Z') &&
                !(character >= L'0' && character <= L'9') &&
                character != L'-' && character != L'.')
            {
                return false;
            }
        }
        return true;
    }

    std::wstring CreateIconFileName(const std::wstring& name, const std::wstring& target)
    {
        uint64_t hash = 1469598103934665603ULL;
        const std::wstring key = name + L"\n" + target;
        for (wchar_t character : key)
        {
            hash ^= static_cast<uint16_t>(character);
            hash *= 1099511628211ULL;
        }
        std::wostringstream stream;
        stream << std::hex << std::setfill(L'0') << std::setw(16) << hash << L".png";
        return stream.str();
    }

    bool SaveShellItemIcon(IShellItem* item, IWICImagingFactory* imagingFactory,
        const std::wstring& outputPath)
    {
        if (GetFileAttributesW(outputPath.c_str()) != INVALID_FILE_ATTRIBUTES)
        {
            return true;
        }
        if (item == nullptr || imagingFactory == nullptr)
        {
            return false;
        }

        IShellItemImageFactory* imageFactory = nullptr;
        HRESULT result = item->QueryInterface(IID_PPV_ARGS(&imageFactory));
        if (FAILED(result))
        {
            return false;
        }

        HBITMAP bitmapHandle = nullptr;
        SIZE size = { 256, 256 };
        result = imageFactory->GetImage(size,
            static_cast<SIIGBF>(SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY), &bitmapHandle);
        imageFactory->Release();
        if (FAILED(result) || bitmapHandle == nullptr)
        {
            return false;
        }

        IWICBitmap* bitmap = nullptr;
        IWICStream* outputStream = nullptr;
        IWICBitmapEncoder* encoder = nullptr;
        IWICBitmapFrameEncode* frame = nullptr;
        IPropertyBag2* properties = nullptr;

        result = imagingFactory->CreateBitmapFromHBITMAP(
            bitmapHandle, nullptr, WICBitmapUseAlpha, &bitmap);
        if (SUCCEEDED(result))
        {
            result = imagingFactory->CreateStream(&outputStream);
        }
        if (SUCCEEDED(result))
        {
            result = outputStream->InitializeFromFilename(outputPath.c_str(), GENERIC_WRITE);
        }
        if (SUCCEEDED(result))
        {
            result = imagingFactory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder);
        }
        if (SUCCEEDED(result))
        {
            result = encoder->Initialize(outputStream, WICBitmapEncoderNoCache);
        }
        if (SUCCEEDED(result))
        {
            result = encoder->CreateNewFrame(&frame, &properties);
        }
        if (SUCCEEDED(result))
        {
            result = frame->Initialize(properties);
        }
        if (SUCCEEDED(result))
        {
            UINT width = 0;
            UINT height = 0;
            result = bitmap->GetSize(&width, &height);
            if (SUCCEEDED(result))
            {
                result = frame->SetSize(width, height);
            }
        }
        if (SUCCEEDED(result))
        {
            WICPixelFormatGUID format = GUID_WICPixelFormat32bppBGRA;
            result = frame->SetPixelFormat(&format);
        }
        if (SUCCEEDED(result))
        {
            result = frame->WriteSource(bitmap, nullptr);
        }
        if (SUCCEEDED(result))
        {
            result = frame->Commit();
        }
        if (SUCCEEDED(result))
        {
            result = encoder->Commit();
        }

        if (properties != nullptr) properties->Release();
        if (frame != nullptr) frame->Release();
        if (encoder != nullptr) encoder->Release();
        if (outputStream != nullptr) outputStream->Release();
        if (bitmap != nullptr) bitmap->Release();
        DeleteObject(bitmapHandle);
        return SUCCEEDED(result);
    }

    bool WriteInstalledAppCatalog(const std::wstring& outputPath, const std::wstring& iconDirectory)
    {
        CreateDirectoryW(iconDirectory.c_str(), nullptr);
        std::wstring temporaryOutputPath = outputPath + L".tmp";

        IShellItem* appsFolder = nullptr;
        HRESULT result = SHCreateItemFromParsingName(
            L"shell:AppsFolder", nullptr, IID_PPV_ARGS(&appsFolder));
        if (FAILED(result))
        {
            return false;
        }

        IEnumShellItems* items = nullptr;
        result = appsFolder->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(&items));
        appsFolder->Release();
        if (FAILED(result))
        {
            return false;
        }

        IWICImagingFactory* imagingFactory = nullptr;
        CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&imagingFactory));

        std::ofstream output(temporaryOutputPath, std::ios::binary | std::ios::trunc);
        if (!output)
        {
            if (imagingFactory != nullptr) imagingFactory->Release();
            items->Release();
            return false;
        }

        ULONG fetched = 0;
        IShellItem* item = nullptr;
        while (items->Next(1, &item, &fetched) == S_OK)
        {
            PWSTR displayName = nullptr;
            PWSTR parsingName = nullptr;
            HRESULT nameResult = item->GetDisplayName(SIGDN_NORMALDISPLAY, &displayName);
            HRESULT targetResult = item->GetDisplayName(SIGDN_PARENTRELATIVEPARSING, &parsingName);
            if (SUCCEEDED(nameResult) && SUCCEEDED(targetResult) &&
                displayName != nullptr && parsingName != nullptr &&
                displayName[0] != L'\0' && parsingName[0] != L'\0')
            {
                std::wstring name(displayName);
                std::wstring target(parsingName);
                std::wstring iconFileName = CreateIconFileName(name, target);
                std::wstring iconPath = iconDirectory + L"\\" + iconFileName;
                if (!SaveShellItemIcon(item, imagingFactory, iconPath))
                {
                    iconFileName.clear();
                }

                output << EncodeBase64(name) << '\t'
                    << EncodeBase64(target) << '\t'
                    << WideToUtf8(iconFileName) << '\n';
            }
            if (displayName != nullptr) CoTaskMemFree(displayName);
            if (parsingName != nullptr) CoTaskMemFree(parsingName);
            item->Release();
            item = nullptr;
        }

        output.flush();
        bool writeSucceeded = output.good();
        output.close();
        if (imagingFactory != nullptr) imagingFactory->Release();
        items->Release();
        if (!writeSucceeded || !MoveFileExW(
            temporaryOutputPath.c_str(),
            outputPath.c_str(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        {
            DeleteFileW(temporaryOutputPath.c_str());
            return false;
        }
        return true;
    }

    HRESULT ActivateApplication(const std::wstring& target, const std::wstring& arguments)
    {
        IApplicationActivationManager* activationManager = nullptr;
        HRESULT result = CoCreateInstance(
            CLSID_ApplicationActivationManager,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&activationManager));
        if (FAILED(result))
        {
            return result;
        }

        DWORD processId = 0;
        result = activationManager->ActivateApplication(
            target.c_str(), arguments.c_str(), AO_NONE, &processId);
        activationManager->Release();
        return result;
    }

    bool IsDirectShellTarget(const std::wstring& target)
    {
        return (target.size() >= 3 && target[1] == L':' &&
                ((target[0] >= L'a' && target[0] <= L'z') ||
                 (target[0] >= L'A' && target[0] <= L'Z'))) ||
            target.compare(0, 2, L"\\\\") == 0 ||
            target.find(L"://") != std::wstring::npos;
    }

    enum class ShortcutLaunchResult
    {
        NotElevatedShortcut,
        Succeeded,
        Failed
    };

    bool EndsWithIgnoreCase(const std::wstring& value, const std::wstring& suffix)
    {
        return value.size() >= suffix.size() &&
            _wcsicmp(value.c_str() + value.size() - suffix.size(), suffix.c_str()) == 0;
    }

    std::wstring ExpandEnvironmentPath(const std::wstring& value)
    {
        if (value.empty())
        {
            return value;
        }
        DWORD required = ExpandEnvironmentStringsW(value.c_str(), nullptr, 0);
        if (required == 0)
        {
            return value;
        }
        std::vector<wchar_t> expanded(required);
        if (ExpandEnvironmentStringsW(value.c_str(), expanded.data(), required) == 0)
        {
            return value;
        }
        return std::wstring(expanded.data());
    }

    ShortcutLaunchResult LaunchElevatedShortcutIfRequested(
        const std::wstring& shortcutPath,
        const std::wstring& additionalArguments)
    {
        if (!EndsWithIgnoreCase(shortcutPath, L".lnk"))
        {
            return ShortcutLaunchResult::NotElevatedShortcut;
        }

        IShellLinkW* shellLink = nullptr;
        HRESULT result = CoCreateInstance(
            CLSID_ShellLink,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&shellLink));
        if (FAILED(result))
        {
            return ShortcutLaunchResult::NotElevatedShortcut;
        }

        IPersistFile* persistFile = nullptr;
        result = shellLink->QueryInterface(IID_PPV_ARGS(&persistFile));
        if (SUCCEEDED(result))
        {
            result = persistFile->Load(shortcutPath.c_str(), STGM_READ);
        }
        if (persistFile != nullptr)
        {
            persistFile->Release();
        }
        if (FAILED(result))
        {
            shellLink->Release();
            return ShortcutLaunchResult::NotElevatedShortcut;
        }

        IShellLinkDataList* dataList = nullptr;
        DWORD flags = 0;
        result = shellLink->QueryInterface(IID_PPV_ARGS(&dataList));
        if (SUCCEEDED(result))
        {
            result = dataList->GetFlags(&flags);
        }
        if (dataList != nullptr)
        {
            dataList->Release();
        }
        if (FAILED(result) || (flags & SLDF_RUNAS_USER) == 0)
        {
            shellLink->Release();
            return ShortcutLaunchResult::NotElevatedShortcut;
        }

        wchar_t targetBuffer[32768] = {};
        wchar_t argumentsBuffer[32768] = {};
        wchar_t workingDirectoryBuffer[32768] = {};
        WIN32_FIND_DATAW targetData = {};
        HRESULT targetResult = shellLink->GetPath(
            targetBuffer,
            ARRAYSIZE(targetBuffer),
            &targetData,
            SLGP_UNCPRIORITY);
        shellLink->GetArguments(argumentsBuffer, ARRAYSIZE(argumentsBuffer));
        shellLink->GetWorkingDirectory(
            workingDirectoryBuffer,
            ARRAYSIZE(workingDirectoryBuffer));
        shellLink->Release();

        std::wstring target = SUCCEEDED(targetResult) && targetBuffer[0] != L'\0'
            ? ExpandEnvironmentPath(targetBuffer)
            : shortcutPath;
        std::wstring parameters = argumentsBuffer;
        if (!additionalArguments.empty())
        {
            if (!parameters.empty())
            {
                parameters += L" ";
            }
            parameters += additionalArguments;
        }
        std::wstring workingDirectory = ExpandEnvironmentPath(workingDirectoryBuffer);
        const wchar_t* parameterPointer = parameters.empty() ? nullptr : parameters.c_str();
        const wchar_t* directoryPointer = workingDirectory.empty()
            ? nullptr
            : workingDirectory.c_str();
        HINSTANCE launchResult = ShellExecuteW(
            nullptr,
            L"runas",
            target.c_str(),
            parameterPointer,
            directoryPointer,
            SW_SHOWNORMAL);
        return reinterpret_cast<INT_PTR>(launchResult) > 32
            ? ShortcutLaunchResult::Succeeded
            : ShortcutLaunchResult::Failed;
    }
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    std::wstring requestPath = GetRequestPath();
    if (requestPath.empty())
    {
        CoUninitialize();
        return 1;
    }

    std::ifstream request(requestPath, std::ios::binary);
    std::string kindLine;
    std::string targetLine;
    std::string argumentsLine;
    if (!ReadLine(request, kindLine) || !ReadLine(request, targetLine))
    {
        CoUninitialize();
        return 2;
    }

    // UWP FileIO may prepend a UTF-8 BOM. Ignore it on the first field and
    // tolerate legacy requests that ended immediately after the target line.
    if (kindLine.size() >= 3 &&
        static_cast<unsigned char>(kindLine[0]) == 0xEF &&
        static_cast<unsigned char>(kindLine[1]) == 0xBB &&
        static_cast<unsigned char>(kindLine[2]) == 0xBF)
    {
        kindLine.erase(0, 3);
    }

    if (!ReadLine(request, argumentsLine))
    {
        argumentsLine.clear();
    }
    request.close();
    DeleteFileW(requestPath.c_str());

    std::wstring kind = Utf8ToWide(kindLine);
    std::wstring target = DecodeBase64(targetLine);
    std::wstring arguments = DecodeBase64(argumentsLine);
    if (kind == L"Catalog")
    {
        std::wstring localStateDirectory = GetParentDirectory(requestPath);
        if (localStateDirectory.empty() || !IsSafeCatalogFileName(target))
        {
            CoUninitialize();
            return 3;
        }
        std::wstring iconDirectory = localStateDirectory + L"\\AppCatalog";
        bool success = WriteInstalledAppCatalog(
            localStateDirectory + L"\\" + target, iconDirectory);
        CoUninitialize();
        return success ? 0 : 4;
    }

    if (target.empty())
    {
        CoUninitialize();
        return 3;
    }

    ShortcutLaunchResult shortcutResult = LaunchElevatedShortcutIfRequested(target, arguments);
    if (shortcutResult != ShortcutLaunchResult::NotElevatedShortcut)
    {
        CoUninitialize();
        return shortcutResult == ShortcutLaunchResult::Succeeded ? 0 : 4;
    }

    if (kind == L"AppId")
    {
        HRESULT activationResult = ActivateApplication(target, arguments);
        CoUninitialize();
        return SUCCEEDED(activationResult) ? 0 : 4;
    }

    if (kind == L"ShellApp")
    {
        if (IsDirectShellTarget(target))
        {
            const wchar_t* parameters = arguments.empty() ? nullptr : arguments.c_str();
            HINSTANCE directResult = ShellExecuteW(
                nullptr, L"open", target.c_str(), parameters, nullptr, SW_SHOWNORMAL);
            CoUninitialize();
            return reinterpret_cast<INT_PTR>(directResult) > 32 ? 0 : 4;
        }

        HRESULT activationResult = ActivateApplication(target, arguments);
        if (SUCCEEDED(activationResult))
        {
            CoUninitialize();
            return 0;
        }

        std::wstring appPath = L"shell:AppsFolder\\" + target;
        HINSTANCE shellResult = ShellExecuteW(
            nullptr, L"open", L"explorer.exe", appPath.c_str(), nullptr, SW_SHOWNORMAL);
        CoUninitialize();
        return reinterpret_cast<INT_PTR>(shellResult) > 32 ? 0 : 4;
    }

    const wchar_t* parameters = arguments.empty() ? nullptr : arguments.c_str();
    HINSTANCE result = ShellExecuteW(nullptr, L"open", target.c_str(), parameters, nullptr, SW_SHOWNORMAL);
    CoUninitialize();
    return reinterpret_cast<INT_PTR>(result) > 32 ? 0 : 4;
}
