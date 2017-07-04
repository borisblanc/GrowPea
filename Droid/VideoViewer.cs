using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace GrowPea.Droid
{
    [Activity(Label = "VideoView", ScreenOrientation = ScreenOrientation.Landscape)]
    public class VideoViewer : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            this.RequestWindowFeature(WindowFeatures.NoTitle);

            this.Window.AddFlags(WindowManagerFlags.Fullscreen);

            SetContentView(Resource.Layout.VideoView);

            var videoView = FindViewById<VideoView>(Resource.Id.VideoView1);
            var path = Intent.GetStringExtra("FilePath");

            if (!string.IsNullOrEmpty(path))
            {
                MediaController mediaController = new MediaController(this);
                mediaController.SetAnchorView(videoView);
                videoView.SetMediaController(mediaController);
                videoView.KeepScreenOn = true;
                videoView.SetVideoPath(path);
                videoView.Start();
                videoView.RequestFocus();
            }
        }
    }
}