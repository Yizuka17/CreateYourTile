#include <windows.h>
#include <appmodel.h>
#include <shlobj.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <wincrypt.h>
#include <fstream>
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
    if (target.empty())
    {
        CoUninitialize();
        return 3;
    }

    if (kind == L"AppId")
    {
        IApplicationActivationManager* activationManager = nullptr;
        HRESULT createResult = CoCreateInstance(
            CLSID_ApplicationActivationManager,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&activationManager));
        if (FAILED(createResult))
        {
            CoUninitialize();
            return 4;
        }

        DWORD processId = 0;
        HRESULT activationResult = activationManager->ActivateApplication(
            target.c_str(),
            arguments.c_str(),
            AO_NONE,
            &processId);
        activationManager->Release();
        CoUninitialize();
        return SUCCEEDED(activationResult) ? 0 : 4;
    }

    const wchar_t* parameters = arguments.empty() ? nullptr : arguments.c_str();
    HINSTANCE result = ShellExecuteW(nullptr, L"open", target.c_str(), parameters, nullptr, SW_SHOWNORMAL);
    CoUninitialize();
    return reinterpret_cast<INT_PTR>(result) > 32 ? 0 : 4;
}
