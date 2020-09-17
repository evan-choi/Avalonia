﻿using System;
using System.Runtime.InteropServices;
using Avalonia.OpenGL.Angle;
using Windows.UI.Composition;
using Windows.UI.Composition.Interop;
using WinRT;

namespace Avalonia.Win32
{
    class CompositionHost
    {
        internal enum DISPATCHERQUEUE_THREAD_APARTMENTTYPE
        {
            DQTAT_COM_NONE = 0,
            DQTAT_COM_ASTA = 1,
            DQTAT_COM_STA = 2
        };

        internal enum DISPATCHERQUEUE_THREAD_TYPE
        {
            DQTYPE_THREAD_DEDICATED = 1,
            DQTYPE_THREAD_CURRENT = 2,
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct DispatcherQueueOptions
        {
            public int dwSize;

            [MarshalAs(UnmanagedType.I4)]
            public DISPATCHERQUEUE_THREAD_TYPE threadType;

            [MarshalAs(UnmanagedType.I4)]
            public DISPATCHERQUEUE_THREAD_APARTMENTTYPE apartmentType;
        };

        [DllImport("coremessaging.dll", EntryPoint = "CreateDispatcherQueueController", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr dispatcherQueueController);

        public static CompositionHost Instance { get; } = new CompositionHost();

        private Compositor _compositor;
        private Windows.System.DispatcherQueueController _dispatcherQueueController;
        private CompositionGraphicsDevice _graphicsDevice;

        private CompositionHost()
        {
            Initialize();
        }

        private void Initialize()
        {
            EnsureDispatcherQueue();
            if (_dispatcherQueueController != null)
                _compositor = new Compositor();

            var interop = _compositor.As<ICompositorInterop>();

            var display = Win32GlManager.EglFeature.Display as AngleWin32EglDisplay;

            _graphicsDevice = interop.CreateGraphicsDevice(display.GetDirect3DDevice());
        }

        public ICompositionDrawingSurfaceInterop InitialiseWindowCompositionTree(IntPtr hwnd, out Windows.UI.Composition.Visual surfaceVisual)
        {
            var target = CreateDesktopWindowTarget(hwnd);

            var surface = _graphicsDevice.CreateDrawingSurface(new Windows.Foundation.Size(0, 0),
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);

            var surfaceInterop = surface.As<ICompositionDrawingSurfaceInterop>();

            var brush = _compositor.CreateSurfaceBrush(surface);
            var visual = _compositor.CreateSpriteVisual();

            visual.Brush = brush;
            visual.Offset = new System.Numerics.Vector3(0, 0, 0);

            var container = _compositor.CreateContainerVisual();
            container.RelativeSizeAdjustment = new System.Numerics.Vector2(1, 1);

            target.Root = container;

            var blur = CreateBlur();

            container.Children.InsertAtTop(blur);

            var visuals = target.Root.As<ContainerVisual>().Children;

            visuals.InsertAtTop(visual);

            visual.CompositeMode = CompositionCompositeMode.SourceOver;

            surfaceVisual = visual;

            return surfaceInterop;
        }

        public SpriteVisual CreateBlur()
        {
            var effect = new GaussianBlurEffect();
            var effectFactory = _compositor.CreateEffectFactory(effect);
            var blurBrush = effectFactory.CreateBrush();

            var backDropBrush = _compositor.CreateBackdropBrush();

            blurBrush.SetSourceParameter("backdrop", backDropBrush);

            var visual = _compositor.CreateSpriteVisual();

            visual.RelativeSizeAdjustment = new System.Numerics.Vector2(1.0f, 1.0f);
            visual.Brush = blurBrush;

            return visual;
        }

        CompositionTarget CreateDesktopWindowTarget(IntPtr window)
        {
            var interop = _compositor.As<global::Windows.UI.Composition.Desktop.ICompositorDesktopInterop>();

            interop.CreateDesktopWindowTarget(window, false, out var windowTarget);
            return Windows.UI.Composition.Desktop.DesktopWindowTarget.FromAbi(windowTarget);
        }

        void EnsureDispatcherQueue()
        {
            if (_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options = new DispatcherQueueOptions();
                options.apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA;
                options.threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));

                CreateDispatcherQueueController(options, out var queue);
                _dispatcherQueueController = Windows.System.DispatcherQueueController.FromAbi(queue);
            }
        }
    }
}

