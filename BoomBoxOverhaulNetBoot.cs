using UnityEngine;

namespace BoomBoxOverhaul
{
    public class BoomBoxOverhaulNetBoot : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BoomBoxOverhaulNet.Initialize(this);
        }
    }
}
