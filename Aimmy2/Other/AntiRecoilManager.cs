﻿using Aimmy2.Class;
using InputLogic;
using System.Windows.Threading;
using Aimmy2.Config;

namespace Other
{
    public class AntiRecoilManager
    {
        public DispatcherTimer HoldDownTimer = new();
        public int IndependentMousePress = 0;

        public void HoldDownLoad()
        {
            if (HoldDownTimer != null)
            {
                HoldDownTimer.Tick += new EventHandler(HoldDownTimerTicker!);
                HoldDownTimer.Interval = TimeSpan.FromMilliseconds(1);
            }
        }

        private void HoldDownTimerTicker(object sender, EventArgs e)
        {
            //Debug.WriteLine(Math.Abs(IndependentMousePress));
            IndependentMousePress += 1;
            if (IndependentMousePress >= AppConfig.Current.AntiRecoilSettings.HoldTime)
                MouseManager.DoAntiRecoil();
        }
    }
}