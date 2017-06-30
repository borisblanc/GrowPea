﻿using Android.App;
using Android.Widget;
using Android.OS;
using Android.Gms.Vision;
using Android.Support.V4.App;


using Android.Util;
using Android;

using Android.Content;
using Android.Gms.Vision.Faces;
using Java.Lang;
using System;
using Android.Runtime;
using Android.Graphics;
using Android.Content.Res;
using Android.Gms.Common;
using System.Threading.Tasks;
using Android.Content.PM;
using System.ComponentModel;



namespace GrowPea.Droid
{
    [Activity(Label = "Growsaic", ScreenOrientation = ScreenOrientation.Landscape)]
    public class Growsaic : Activity
    {

        private static readonly string TAG = "Growsaic";

        private CameraSource mCameraSource = null;

        private CameraSourcePreview mPreview;
        private GraphicOverlay mGraphicOverlay;

        private Button mRecbutton;

        private static readonly int RC_HANDLE_GMS = 9001;

        private bool isRecording = false;
        private CustomFaceDetector myFaceDetector;

        private int pFramewidth = 640;
        private int pFrameHeight = 480;

        private Single Fps = 30.0f;
        private int vidlengthseconds = 3;

        private const int framemin = 300; //minimum number of frames needed to process this


        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            this.Window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
            
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Growsaic);

            mPreview = FindViewById<CameraSourcePreview>(Resource.Id.preview);
            mGraphicOverlay = FindViewById<GraphicOverlay>(Resource.Id.faceOverlay);
            mRecbutton = FindViewById<Button>(Resource.Id.btnRecord);

            mRecbutton.Click += (sender, e) => SetRecording();

            CreateCameraSource();
        }


        private void SetRecording()
        {
            if (!isRecording)
            {
                isRecording = true;
                mGraphicOverlay.isRecording = true;
                myFaceDetector.isRecording = true;
                mRecbutton.Text = "STOP";
                mRecbutton.SetTextColor(Color.Red);
            }
            else
            {
                isRecording = false;
                mGraphicOverlay.isRecording = false;
                myFaceDetector.isRecording = false;
                mRecbutton.Text = "RECORD";
                mRecbutton.SetTextColor(Color.Black);

                if (myFaceDetector._allFrameData.Count > 0)
                {
                    StartFrameProcessing();
                }
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
                    .SetFacing(CameraFacing.Front)
                    .SetRequestedFps(Fps)
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

            var toast = Toast.MakeText(this, "Processing Smiles :)!", ToastLength.Short);
            toast.Show();

            bool result= false;

            if (myFaceDetector._allFrameData.Count >= framemin) 
            {
                var fdp = new FrameDataProcessor(myFaceDetector._allFrameData, pFramewidth, pFrameHeight, (int)Fps, vidlengthseconds);
                result = await fdp.BeginProcessingFrames();
            }
            else
            {
                toast = Toast.MakeText(this, "Need a longer video!! Try Again.", ToastLength.Short);
                toast.Show();
                return;
            }


            if (result)
            {
                toast = Toast.MakeText(this, "Smiles Processed:)!", ToastLength.Short);
                toast.Show();
            }
            else
            {

                toast = Toast.MakeText(this, "Error with Smiles:(!", ToastLength.Short);
                toast.Show();

            }
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

