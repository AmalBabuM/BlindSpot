using UnityEngine;

namespace Clearance
{
    [CreateAssetMenu(menuName = "Clearance/Scoring Rules")]
    public sealed class ScoringRules : ScriptableObject
    {
        public int cornerChecked = 50;
        public int commandAccepted = 80;
        public int restraint = 150;
        public int nonLethal = 150;
        public int preciseNonLethal = 100;
        public int roomSecured = 100;
        public int recklessEntry = -100;
        public int unjustifiedShot = -300;
        public int justifiedShot = 50;
        public int unnecessaryForce = -250;
    }
}
