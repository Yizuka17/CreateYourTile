using System;

namespace CreateYourTile.Uwp.Services
{
    internal static class ImagePositioning
    {
        public static double CalculateStart(
            double viewportDimension,
            double imageDimension,
            double normalizedOffset)
        {
            normalizedOffset = Math.Max(-1, Math.Min(1, normalizedOffset));
            double travel = Math.Abs(imageDimension - viewportDimension);
            return (viewportDimension - imageDimension) / 2 -
                normalizedOffset * travel / 2;
        }
    }
}
