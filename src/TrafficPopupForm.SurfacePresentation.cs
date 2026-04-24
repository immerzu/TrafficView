using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal sealed partial class TrafficPopupForm
    {
        private bool EnsureSurfaceBitmaps()
        {
            if (this.staticSurfaceBitmap != null &&
                this.staticSurfaceBitmap.Width == this.Width &&
                this.staticSurfaceBitmap.Height == this.Height &&
                this.composedSurfaceBitmap != null &&
                this.composedSurfaceBitmap.Width == this.Width &&
                this.composedSurfaceBitmap.Height == this.Height)
            {
                return true;
            }

            this.DisposeSurfaceBitmaps();

            try
            {
                this.staticSurfaceBitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
                this.composedSurfaceBitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
                this.staticSurfaceDirty = true;
                this.lastRenderedAnimationFrame = -1;
                this.lastRenderedTrafficHistoryVersion = -1;
                return true;
            }
            catch (ExternalException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-surface-bitmap-external-exception",
                    "Overlay surface bitmap creation failed because of a GDI/native rendering exception.",
                    ex);
            }
            catch (ArgumentException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-surface-bitmap-argument-exception",
                    "Overlay surface bitmap creation failed because of invalid dimensions or pixel format.",
                    ex);
            }
            catch (OutOfMemoryException ex)
            {
                this.HandleOverlayRenderFailure(
                    "overlay-surface-bitmap-oom",
                    "Overlay surface bitmap creation failed because memory was not available.",
                    ex);
            }

            return false;
        }

        private bool PresentLayeredBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return false;
            }

            IntPtr screenDc = IntPtr.Zero;
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                screenDc = GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero)
                {
                    AppLog.WarnOnce(
                        "overlay-getdc-failed",
                        string.Format(
                            "GetDC failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                {
                    AppLog.WarnOnce(
                        "overlay-createcompatibledc-failed",
                        string.Format(
                            "CreateCompatibleDC failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                if (hBitmap == IntPtr.Zero)
                {
                    AppLog.WarnOnce(
                        "overlay-gethbitmap-failed",
                        string.Format(
                            "GetHbitmap failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                oldBitmap = SelectObject(memoryDc, hBitmap);
                if (IsInvalidSelectObjectResult(oldBitmap))
                {
                    AppLog.WarnOnce(
                        "overlay-selectobject-failed",
                        string.Format(
                            "SelectObject failed for layered overlay rendering. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
                NativePoint sourcePoint = new NativePoint(0, 0);
                NativePoint topPosition = new NativePoint(this.Left, this.Top);
                BlendFunction blend = new BlendFunction();
                blend.BlendOp = AcSrcOver;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AcSrcAlpha;

                if (!UpdateLayeredWindow(
                    this.Handle,
                    screenDc,
                    ref topPosition,
                    ref size,
                    memoryDc,
                    ref sourcePoint,
                    0,
                    ref blend,
                    UlwAlpha))
                {
                    AppLog.WarnOnce(
                        "overlay-updatelayeredwindow-failed",
                        string.Format(
                            "UpdateLayeredWindow failed for overlay presentation. Win32={0}",
                            Marshal.GetLastWin32Error()));
                    return false;
                }

                return true;
            }
            catch (ExternalException ex)
            {
                AppLog.WarnOnce("overlay-gdi-external-exception", "GDI/bitmap operation failed during overlay presentation.", ex);
                return false;
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                {
                    IntPtr restoreResult = SelectObject(memoryDc, oldBitmap);
                    if (IsInvalidSelectObjectResult(restoreResult))
                    {
                        AppLog.WarnOnce(
                            "overlay-selectobject-restore-failed",
                            string.Format(
                                "SelectObject failed while restoring the previous overlay bitmap object. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }

                if (hBitmap != IntPtr.Zero)
                {
                    if (!DeleteObject(hBitmap))
                    {
                        AppLog.WarnOnce(
                            "overlay-deleteobject-failed",
                            string.Format(
                                "DeleteObject failed while releasing overlay bitmap resources. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }

                if (memoryDc != IntPtr.Zero)
                {
                    if (!DeleteDC(memoryDc))
                    {
                        AppLog.WarnOnce(
                            "overlay-deletedc-failed",
                            string.Format(
                                "DeleteDC failed while releasing overlay memory DC. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }

                if (screenDc != IntPtr.Zero)
                {
                    if (ReleaseDC(IntPtr.Zero, screenDc) == 0)
                    {
                        AppLog.WarnOnce(
                            "overlay-releasedc-failed",
                            string.Format(
                                "ReleaseDC failed while releasing overlay screen DC. Win32={0}",
                                Marshal.GetLastWin32Error()));
                    }
                }
            }
        }

        private void HandleOverlayRenderFailure(string key, string message, Exception exception)
        {
            this.DisposeSurfaceBitmaps();
            this.staticSurfaceDirty = true;
            this.lastRenderedAnimationFrame = -1;
            this.lastRenderedTrafficHistoryVersion = -1;
            this.lastPresentedLocation = new Point(int.MinValue, int.MinValue);
            this.lastPresentedSize = Size.Empty;
            AppLog.WarnOnce(key, message, exception);
        }

        private static bool IsInvalidSelectObjectResult(IntPtr result)
        {
            return result == IntPtr.Zero || result == new IntPtr(-1);
        }

        private void DisposeSurfaceBitmaps()
        {
            if (this.staticSurfaceBitmap != null)
            {
                this.staticSurfaceBitmap.Dispose();
                this.staticSurfaceBitmap = null;
            }

            if (this.composedSurfaceBitmap != null)
            {
                this.composedSurfaceBitmap.Dispose();
                this.composedSurfaceBitmap = null;
            }
        }
    }
}
