using System.Globalization;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    public sealed class HerdBoostSettings : IExposable
    {
        public float output = 1f;
        public float breeding = 1f;
        public float gestation = 1f;
        public bool IsDefault => output == 1f && breeding == 1f && gestation == 1f;
        public string Key => output.ToString("R", CultureInfo.InvariantCulture) + "/" + breeding.ToString("R", CultureInfo.InvariantCulture) + "/" + gestation.ToString("R", CultureInfo.InvariantCulture);
        public HerdBoostSettings Clone() => new HerdBoostSettings { output = output, breeding = breeding, gestation = gestation };
        public void ExposeData()
        {
            Scribe_Values.Look(ref output, "output", 1f);
            Scribe_Values.Look(ref breeding, "breeding", 1f);
            Scribe_Values.Look(ref gestation, "gestation", 1f);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            output = BoostMath.Sanitize(output); breeding = BoostMath.Sanitize(breeding); gestation = BoostMath.Sanitize(gestation);
        }
    }
    public sealed class HerdSpeciesBoost : IExposable
    {
        public string defName;
        public HerdBoostSettings boosts = new HerdBoostSettings();
        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Deep.Look(ref boosts, "boosts");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && boosts == null) boosts = new HerdBoostSettings();
        }
    }
}
