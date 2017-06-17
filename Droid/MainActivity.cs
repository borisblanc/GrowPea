using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Graphics;
using Java.Lang;
using System.Collections.Generic;
using Android.Media;


namespace GrowPea.Droid
{
    [Activity(Label = "GrowPea", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity, TextureView.ISurfaceTextureListener
    {
        CameraDevice _camera;
		TextureView _textureView;

		//{@link CaptureRequest.Builder} for the camera preview
		CaptureRequest.Builder mPreviewRequestBuilder;

        // An {@link ImageReader} that handles still image capture.
        ImageReader mImageReader;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

			_textureView = new TextureView(this);
			_textureView.SurfaceTextureListener = this;

			SetContentView(_textureView);
  
        }


		public void OnSurfaceTextureAvailable(SurfaceTexture currentsurface, int w, int h)
		{
            //_camera.Open();

			_textureView.LayoutParameters = new FrameLayout.LayoutParams(w, h);

			//try
			//{
			//	_camera.SetPreviewTexture(surface);
			//	_camera.StartPreview();

			//}
			//catch (Java.IO.IOException ex)
			//{
			//	System.Diagnostics.Debug.WriteLine(ex.Message);
			//}

			try
			{
				mImageReader = ImageReader.NewInstance(w, h, ImageFormatType.Jpeg, /*maxImages*/2);
				//ImageReader.SetOnImageAvailableListener(mOnImageAvailableListener, mBackgroundHandler);

				SurfaceTexture texture = currentsurface;
				if (texture == null)
				{
					throw new IllegalStateException("texture is null");
				}

				// We configure the size of default buffer to be the size of camera preview we want.
				texture.SetDefaultBufferSize(w, h);

				// This is the output Surface we need to start preview.
				Surface surface = new Surface(texture);

				// We set up a CaptureRequest.Builder with the output Surface.
				mPreviewRequestBuilder = _camera.CreateCaptureRequest(CameraTemplate.Preview);
				mPreviewRequestBuilder.AddTarget(surface);

				// Here, we create a CameraCaptureSession for camera preview.
				var surfaces = new List<Surface>();
				surfaces.Add(surface);
				surfaces.Add(mImageReader.Surface);
				_camera.CreateCaptureSession(surfaces, null, null);

			}
			catch (CameraAccessException e)
			{
				e.PrintStackTrace();
			}
		}

        public void OnSurfaceTextureUpdated(Android.Graphics.SurfaceTexture surface)
        {


        }

		public void OnSurfaceTextureSizeChanged(Android.Graphics.SurfaceTexture surface, int w, int h)
		{


		}

		public bool OnSurfaceTextureDestroyed(Android.Graphics.SurfaceTexture surface)
		{
            _camera.Close();
		

			return true;
		}




    }


}

