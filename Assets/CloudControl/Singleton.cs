using UnityEngine;
using System.Collections;

namespace Baidu.VR
{
    public class Singleton<T> where T:new(){
        private static T _instance;

        private static object _lock = new object();

        public static T Instance{
            get{
                lock (_lock){
                    if (ReferenceEquals(_instance, null)){
                        _instance = new T();
                    }

                    return _instance;
                }
            }
        }

        public static bool IsInstanceValid { get { return !ReferenceEquals(_instance, null); } }
    }

    public class Singleton2<T> : MonoBehaviour where T : MonoBehaviour{
        private static T _instance;
        private static GameObject _gameObject;

        private static object _lock = new object();

        void OnDestroy(){
            Destroy();
        }

        public void Destroy(){
            if (_instance == this)
                _instance = null;
            if (_gameObject == gameObject)
                _gameObject = null;
        }

        public static T Instance{
            get{
                lock (_lock){
                    if (ReferenceEquals(_instance, null))
                    {
					    T[] instances = Resources.FindObjectsOfTypeAll (typeof(T)) as T[];
					    for (int i = 0; i < instances.Length; ++i) {
						    if (instances [i].gameObject.scene.name == null)
							    continue;

                            if (instances[i].gameObject.activeInHierarchy)
                            {
                                _instance = instances[i];
                                break;
                            }else if(ReferenceEquals(_instance, null))
                                _instance = instances[i];
                        }

					    if (ReferenceEquals(_instance, null)) {
						    GameObject singleton = _gameObject ? _gameObject : new GameObject ();
						    _instance = singleton.GetComponent<T> ();
						    if (!_instance) {
							    _instance = singleton.AddComponent<T> ();
						    }
						    singleton.name = "(singleton) " + typeof(T).ToString ();
					    } else {
						    _gameObject = _instance.gameObject;
					    }
                    }

                    return _instance;
                }
            }
        }

	    public static bool IsInstanceValid{get{ return !ReferenceEquals(_instance, null); }}
    }

    public class SingletonDontDestroy<T> : MonoBehaviour where T : MonoBehaviour{
	    private static T _instance;
	    private static GameObject _gameObject;

	    private static object _lock = new object();

	    void OnDestroy(){
            Destroy();
        }

        public void Destroy(){
            if (_instance == this)
                _instance = null;
            if (_gameObject == gameObject)
                _gameObject = null;
        }

        public static T Instance{
		    get{
			    lock (_lock){
				    if (ReferenceEquals(_instance, null))
                    {
					    T[] instances = Resources.FindObjectsOfTypeAll (typeof(T)) as T[];
					    for (int i = 0; i < instances.Length; ++i) {
						    if (instances [i].gameObject.scene.name == null)
							    continue;

                            if (instances[i].gameObject.activeInHierarchy)
                            {
                                _instance = instances[i];
                                break;
                            }
                            else if (ReferenceEquals(_instance, null))
                                _instance = instances[i];
                        }

					    if (ReferenceEquals(_instance, null)) {
						    GameObject singleton = _gameObject ? _gameObject : new GameObject ();
						    singleton.name = "(singleton) " + typeof(T).ToString ();
						    _instance = singleton.GetComponent<T> ();
						    if (!_instance) {
							    _instance = singleton.AddComponent<T> ();
						    }
                            if (Application.isPlaying)
                                DontDestroyOnLoad(singleton);
                        } else {
						    _gameObject = _instance.gameObject;
					    }
				    }

				    return _instance;
			    }
		    }
	    }
    }
}
