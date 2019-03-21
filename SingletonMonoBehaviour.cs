using UnityEngine;

namespace ChyLib
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T _Instance = null;

        public static T Instance
        {
            get { return GetInstance(); }
        }

        public static T GetInstance()
        {
            if (_Instance == null)
            {
                _Instance = Object.FindObjectOfType<T>();
                if (_Instance == null)
                {
                    GameObject gameObject = new GameObject(typeof(T).Name);
                    _Instance = gameObject.AddComponent<T>();
                }
            }
            return _Instance;
        }

        public static bool IsInstanced { get { return _Instance != null; } }

        [SerializeField]
        private bool _isDontDestroy = true;

        private void Awake()
        {
            if (_Instance == null)
            {
                _Instance = gameObject.GetComponent<T>();
            }
            else if (_Instance != this)
            {
                _Instance.OnDestroy();
                _Instance = gameObject.GetComponent<T>();
            }

            if (_isDontDestroy)
            {
                DontDestroyOnLoad(this);
            }
            OnInitialize();
        }

        private void OnDestroy()
        {
            if (this == _Instance)
            {
                _Instance = null;
            }

            OnFinalize();
            Destroy(this);
        }

        /// <summary>
        /// 初期化のときに呼ばれます
        /// </summary>
        protected virtual void OnInitialize()
        {

        }

        /// <summary>
        /// 破棄されるときに呼ばれます
        /// </summary>
        protected virtual void OnFinalize()
        {

        }
    }
}
