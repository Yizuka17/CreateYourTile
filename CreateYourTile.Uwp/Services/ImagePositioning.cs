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
            double naturalTravel = Math.Abs(imageDimension - viewportDimension);
            // When the image exactly fills (or almost fills) the viewport, the
            // natural travel is zero (or too small to notice). Keep a modest
            // manual positioning range so the sliders remain useful at 1.0x.
            // At the extremes, at least half of the shorter dimension remains
            // visible instead of allowing the image to be moved completely out.
            double manualTravel = Math.Min(viewportDimension, imageDimension) / 2;
            double travel = Math.Max(naturalTravel, manualTravel);
            return (viewportDimension - imageDimension) / 2 -
                normalizedOffset * travel / 2;
        }
    }
}
