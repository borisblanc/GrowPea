using Android.App;
using Android.Widget;
using Android.OS;
using Android.Gms.Vision;
using Android.Util;
using Android.Gms.Vision.Faces;
using System;
using System.Collections.Generic;
using Android.Graphics;
using Android.Gms.Common;
using Android.Content.PM;
using System.ComponentModel;
using Android.Views;
using Java.Nio;


namespace GrowPea.Droid
{
    [Activity(Label = "VideoActivity", ScreenOrientation = ScreenOrientation.Landscape)]
    public class VideoActivity : Activity
    {

        private static readonly string TAG = "VideoActivity";

        private CameraSource mCameraSource = null;

        private CameraSourcePreview mPreview;
        private GraphicOverlay mGraphicOverlay;

        private Button mRecbutton;
        private ImageButton mSwitchcamButton;

        private static readonly int RC_HANDLE_GMS = 9001;

        private bool isRecording = false;
        private CustomFaceDetector myFaceDetector;

        private int pFramewidth = 640;
        private int pFrameHeight = 480;

        private Single _recordFps = 60.0f;
        private int _createfps = 30;
        private int vidlengthseconds = 3;

        private const int framemin = 200; //minimum number of frames needed to process this

        private CameraFacing camface = CameraFacing.Front; //default may change

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            this.RequestWindowFeature(WindowFeatures.NoTitle);

            this.Window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
            
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Growsaic);

            mPreview = FindViewById<CameraSourcePreview>(Resource.Id.preview);
            mGraphicOverlay = FindViewById<GraphicOverlay>(Resource.Id.faceOverlay);
            mRecbutton = FindViewById<Button>(Resource.Id.btnRecord);
            mSwitchcamButton = FindViewById<ImageButton>(Resource.Id.btnswCam);

            mRecbutton.Click += (sender, e) => ToggleRecording();
            mSwitchcamButton.Click += (sender, e) => ToggleCamface();

            CreateCameraSource();
        }


        private void ToggleRecording()
        {
            if (!isRecording)
            {
                isRecording = true;
                mGraphicOverlay.isRecording = true;
                myFaceDetector.isRecording = true;
                mRecbutton.Text = "STOP";
                mRecbutton.SetTextColor(Color.Red);
                mSwitchcamButton.Enabled = false;
            }
            else
            {
                isRecording = false;
                mGraphicOverlay.isRecording = false;
                myFaceDetector.isRecording = false;
                mRecbutton.Text = "RECORD";
                mRecbutton.SetTextColor(Color.Black);
                mSwitchcamButton.Enabled = true;

                if (myFaceDetector._allFrameData.Count > 0)
                {
                    StartFrameProcessing();
                }
            }
        }

        private void ToggleCamface()
        {
            if (camface == CameraFacing.Front)
            {
                camface = CameraFacing.Back;
                mCameraSource.Release();
                mPreview.Stop();
                CreateCameraSource();
                StartCameraSource();
            }
            else
            {
                camface = CameraFacing.Front;
                mCameraSource.Release();
                mPreview.Stop();
                CreateCameraSource();
                StartCameraSource();
            }
        }



        private void CreateCameraSource()
        {

            var context = Application.Context;
            FaceDetector detector = new FaceDetector.Builder(context)
                    .SetTrackingEnabled(true)
                    .SetClassificationType(ClassificationType.All)
                    .SetProminentFaceOnly(true)
                    .Build();

            myFaceDetector = new CustomFaceDetector(detector);

            myFaceDetector.PropertyChanged += OnPropertyChanged;

            myFaceDetector.SetProcessor(
                    new LargestFaceFocusingProcessor.Builder(myFaceDetector, new GraphicFaceTracker(this.mGraphicOverlay))
                            .Build());

            if (!myFaceDetector.IsOperational)
            {
                // isOperational() can be used to check if the required native library is currently
                // available.  The detector will automatically become operational once the library
                // download completes on device.
                Log.Warn(TAG, "Face detector dependencies are not yet available.");
            }


            mCameraSource = new CameraSource.Builder(context, myFaceDetector)
                    .SetRequestedPreviewSize(pFramewidth, pFrameHeight)
                    .SetFacing(camface)
                    .SetRequestedFps(_recordFps)
                    .SetAutoFocusEnabled(true)
                    .Build();

        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "isRecording" && !myFaceDetector.isRecording)
            {
                isRecording = false;
                mGraphicOverlay.isRecording = false;
                this.RunOnUiThread(() => //can only do this on UI thread
                {
                    mRecbutton.Text = "RECORD";
                    mRecbutton.SetTextColor(Color.Black);

                    Toast toast = Toast.MakeText(this, "Smiles Captured :)!", ToastLength.Short);
                    toast.Show();
                });

                //if (myFaceDetector._allFrameData.Count > 0)
                //{
                //    StartFrameProcessing();
                //}
            }
        }

        private async void StartFrameProcessing()
        {
            Showpopup("Processing Smiles :)!", ToastLength.Short);

            List<ByteBuffer> images;
            bool result;

            if (myFaceDetector._allFrameData.Count >= framemin) 
            {
                var fdp = new FrameDataProcessor(myFaceDetector._allFrameData, pFramewidth, pFrameHeight, _createfps, vidlengthseconds);
                images = await fdp.BeginProcessingFrames();

                if (images == null)
                {
                    Showpopup("Error with Smiles processing :(!", ToastLength.Short);
                }
                else
                {
                    Showpopup("Smiles processed :)!", ToastLength.Short);
                    result = await fdp.BeginMakeBufferVideo(images);

                    Showpopup(result ? "Video Created :)!" : "Error with video (!", ToastLength.Short);
                }
            }
            else
            {
                Showpopup("Need a longer video!! Try Again.", ToastLength.Short);
            }
        }

        private void Showpopup(string msg, ToastLength length)
        {
            var toast = Toast.MakeText(this, msg, length);
            toast.Show();
        }
            

        protected override void OnResume()
        {
            base.OnResume();
            StartCameraSource();
        }

        protected override void OnPause()
        {
            base.OnPause();
            mPreview.Stop();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (mCameraSource != null)
            {
                mCameraSource.Release();
            }
        }

        /**
   * Starts or restarts the camera source, if it exists.  If the camera source doesn't exist yet
   * (e.g., because onResume was called before the camera source was created), this will be called
   * again when the camera source is created.
   */
        private void StartCameraSource()
        {

            // check that the device has play services available.
            int code = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this.ApplicationContext);
            if (code != ConnectionResult.Success)
            {
                Dialog dlg = GoogleApiAvailability.Instance.GetErrorDialog(this, code, RC_HANDLE_GMS);
                dlg.Show();
            }

            if (mCameraSource != null)
            {
                try
                {
                    mPreview.Start(mCameraSource, mGraphicOverlay);
                }
                catch (System.Exception e)
                {
                    Log.Error(TAG, "Unable to start camera source.", e);
                    mCameraSource.Release();
                    mCameraSource = null;
                }
            }
        }

        class GraphicFaceTracker : Tracker
        {
            private GraphicOverlay mOverlay;
            private FaceGraphic mFaceGraphic;

            public GraphicFaceTracker(GraphicOverlay overlay)
            {
                mOverlay = overlay;
                mFaceGraphic = new FaceGraphic(overlay);
            }

            public override void OnNewItem(int id, Java.Lang.Object item)
            {
                mFaceGraphic.SetId(id);
            }

            public override void OnUpdate(Detector.Detections detections, Java.Lang.Object item)
            {
                 
                var face = item as Face;
                mOverlay.Add(mFaceGraphic);
                mFaceGraphic.UpdateFace(face);
            }

            public override void OnMissing(Detector.Detections detections)
            {
                mOverlay.Remove(mFaceGraphic);
            }

            public override void OnDone()
            {
                mOverlay.Remove(mFaceGraphic);
            }
        }



    }


}

