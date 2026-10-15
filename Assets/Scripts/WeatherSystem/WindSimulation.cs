using System;

namespace MagicIsland.WeatherSystem
{
    // Cosmetic wind advances only with the outdoor simulation, independent of save/gameplay randomness.
    public sealed class WindSimulation
    {
        public double Time { get; private set; }
        public double Strength { get; private set; }
        private double baseline;
        private readonly double sunny,rainy,gust,transition,phase;
        public WindSimulation(double sunny,double rainy,double gust,double transition,int seed,bool raining)
        {
            this.sunny=sunny;this.rainy=rainy;this.gust=gust;this.transition=transition;
            phase=(seed%997)*.071;baseline=raining?rainy:sunny;Strength=baseline;
        }
        public void Tick(double seconds,bool raining)
        {
            if(seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds))return;
            Time+=seconds;double target=raining?rainy:sunny;
            baseline+=(target-baseline)*(1-Math.Exp(-seconds/transition));
            double pulse=.65*Math.Sin(Time*.37+phase)+.35*Math.Sin(Time*.91+phase*1.7);
            Strength=Math.Max(0,baseline*(1+gust*pulse));
        }
    }
}
