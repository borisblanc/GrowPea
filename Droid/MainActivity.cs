using Android.App;
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
    [Activity(Label = "GrowPea", MainLauncher = true, Icon = "@mipmap/icon", ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {

        private static readonly string TAG = "FaceTracker";

        private CameraSource mCameraSource = null;

        private CameraSourcePreview mPreview;
        private GraphicOverlay mGraphicOverlay;

        private Button mRecbutton;

        private static readonly int RC_HANDLE_GMS = 9001;

        private bool isRecording = false;
        private CustomFaceDetector myFaceDetector;

        //public static string GreetingsText
        //{
        //    get;
        //    set;
        //}



        //public Tracker Create(Java.Lang.Object item)
        //{
        //    return new GraphicFaceTracker(mGraphicOverlay);
        //}

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            this.Window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
            
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            mPreview = FindViewById<CameraSourcePreview>(Resource.Id.preview);
            mGraphicOverlay = FindViewById<GraphicOverlay>(Resource.Id.faceOverlay);
            mRecbutton = FindViewById<Button>(Resource.Id.btnRecord);
            //greetingsText = FindViewById<TextView>(Resource.Id.greetingsTextView);



            mRecbutton.Click += (sender, e) => SetRecording();



            //if (ActivityCompat.CheckSelfPermission(this, Manifest.Permission.Camera) == Permission.Granted)
            //{
            CreateCameraSource();
                //LiveCamHelper.Init();
                //LiveCamHelper.GreetingsCallback = (s) => { RunOnUiThread(() => GreetingsText = s); };
                //await LiveCamHelper.RegisterFaces();
            //}
            //else { RequestCameraPermission(); }

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
                // Note: The first time that an app using face API is installed on a device, GMS will
                // download a native library to the device in order to do detection.  Usually this
                // completes before the app is run for the first time.  But if that download has not yet
                // completed, then the above call will not detect any faces.
                //
                // isOperational() can be used to check if the required native library is currently
                // available.  The detector will automatically become operational once the library
                // download completes on device.
                Log.Warn(TAG, "Face detector dependencies are not yet available.");
            }


            mCameraSource = new CameraSource.Builder(context, myFaceDetector)
                    .SetRequestedPreviewSize(640, 480)
                    .SetFacing(CameraFacing.Front)
                    .SetRequestedFps(30.0f)
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
                });
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

