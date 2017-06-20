using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;

namespace GrowPea.Droid
{
    [Activity(Label = "SayCheese", MainLauncher = true, Icon = "@drawable/Cheese", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainMenu : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.MainMenu);
            // Create your application here

            var btnGrowsaic = FindViewById<Button>(Resource.Id.btn_Growsaic);

            btnGrowsaic.Click += (object sender, EventArgs e) =>
            {
                var intent = new Intent(this, typeof(Growsaic));
                StartActivity(intent);
            };
        }
    }
}