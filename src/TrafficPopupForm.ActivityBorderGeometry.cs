using System;
using System.Drawing;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private static double GetActivityBorderFadeRatio(double intensity)
        {
            if (intensity <= ActivityBorderFadeInStartRatio)
            {
                return 0D;
            }

            double normalized = (intensity - ActivityBorderFadeInStartRatio) /
                Math.Max(0.0001D, ActivityBorderFadeInFullRatio - ActivityBorderFadeInStartRatio);
            return SmoothStep(Math.Max(0D, Math.Min(1D, normalized)));
        }

        private static double GetActivityBorderTravelAccent(double unitPosition, double phase, double direction)
        {
            double forwardDistance = direction >= 0D
                ? GetCircularForwardDistance(phase, unitPosition)
                : GetCircularForwardDistance(unitPosition, phase);
            double head = 1D - Math.Min(1D, forwardDistance / ActivityBorderTravelPulseWidth);
            double tail = 1D - Math.Min(1D, forwardDistance / ActivityBorderTravelTailWidth);
            double secondaryPhase = phase + 0.50D;
            secondaryPhase -= Math.Floor(secondaryPhase);
            double secondaryDistance = direction >= 0D
                ? GetCircularForwardDistance(secondaryPhase, unitPosition)
                : GetCircularForwardDistance(unitPosition, secondaryPhase);
            double secondaryHead = 1D - Math.Min(1D, secondaryDistance / (ActivityBorderTravelPulseWidth * 0.86D));

            return Math.Max(
                SmoothStep(head),
                Math.Max(SmoothStep(tail) * 0.54D, SmoothStep(secondaryHead) * 0.58D));
        }

        private static double GetCircularForwardDistance(double fromUnitPosition, double toUnitPosition)
        {
            double distance = toUnitPosition - fromUnitPosition;
            distance -= Math.Floor(distance);
            return distance;
        }

        private static float GetRoundedRectanglePerimeter(RectangleF bounds, float radius)
        {
            float safeRadius = Math.Max(0F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2F));
            float horizontal = Math.Max(0F, bounds.Width - (2F * safeRadius));
            float vertical = Math.Max(0F, bounds.Height - (2F * safeRadius));
            return (2F * horizontal) + (2F * vertical) + ((float)(Math.PI * 2D) * safeRadius);
        }

        private static PointF GetRoundedRectanglePoint(RectangleF bounds, float radius, double unitPosition)
        {
            float safeRadius = Math.Max(0F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2F));
            float horizontal = Math.Max(0F, bounds.Width - (2F * safeRadius));
            float vertical = Math.Max(0F, bounds.Height - (2F * safeRadius));
            float arcLength = (float)(Math.PI * safeRadius / 2D);
            float perimeter = (2F * horizontal) + (2F * vertical) + (4F * arcLength);
            if (perimeter <= 0F)
            {
                return new PointF(bounds.Left + (bounds.Width / 2F), bounds.Top + (bounds.Height / 2F));
            }

            double normalizedUnit = unitPosition - Math.Floor(unitPosition);
            float distance = (float)(normalizedUnit * perimeter);

            if (distance <= horizontal)
            {
                return new PointF(bounds.Left + safeRadius + distance, bounds.Top);
            }

            distance -= horizontal;
            if (distance <= arcLength)
            {
                return GetPointOnCircle(
                    bounds.Right - safeRadius,
                    bounds.Top + safeRadius,
                    safeRadius,
                    -90D + ((distance / Math.Max(0.0001F, arcLength)) * 90D));
            }

            distance -= arcLength;
            if (distance <= vertical)
            {
                return new PointF(bounds.Right, bounds.Top + safeRadius + distance);
            }

            distance -= vertical;
            if (distance <= arcLength)
            {
                return GetPointOnCircle(
                    bounds.Right - safeRadius,
                    bounds.Bottom - safeRadius,
                    safeRadius,
                    (distance / Math.Max(0.0001F, arcLength)) * 90D);
            }

            distance -= arcLength;
            if (distance <= horizontal)
            {
                return new PointF(bounds.Right - safeRadius - distance, bounds.Bottom);
            }

            distance -= horizontal;
            if (distance <= arcLength)
            {
                return GetPointOnCircle(
                    bounds.Left + safeRadius,
                    bounds.Bottom - safeRadius,
                    safeRadius,
                    90D + ((distance / Math.Max(0.0001F, arcLength)) * 90D));
            }

            distance -= arcLength;
            if (distance <= vertical)
            {
                return new PointF(bounds.Left, bounds.Bottom - safeRadius - distance);
            }

            distance -= vertical;
            return GetPointOnCircle(
                bounds.Left + safeRadius,
                bounds.Top + safeRadius,
                safeRadius,
                180D + ((distance / Math.Max(0.0001F, arcLength)) * 90D));
        }

        private static PointF GetPointOnCircle(float centerX, float centerY, float radius, double angleDegrees)
        {
            double angleRadians = angleDegrees * Math.PI / 180D;
            return new PointF(
                centerX + ((float)Math.Cos(angleRadians) * radius),
                centerY + ((float)Math.Sin(angleRadians) * radius));
        }
    }
}
