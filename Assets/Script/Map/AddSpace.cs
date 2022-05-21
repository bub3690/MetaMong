	using UnityEngine;
	using Mapbox.Utils;
	using Mapbox.Unity.Map;
	using Mapbox.Unity.MeshGeneration.Factories;
	using Mapbox.Unity.Utilities;
	using System.Collections.Generic;
	using Cysharp.Threading.Tasks;

public class AddSpace : MonoBehaviour
{    
    /*
        AR SPACE를 불러오는 CLASS.
		여러 circle instance를 만들어준다.
        추후에, 통신이나 json을 불러오는 것으로 수정.
        https://docs.mapbox.com/unity/maps/guides/add-markers/#use-the-spawnonmap-script

    */
		[SerializeField]
		AbstractMap _map;

		AuthManager auth;
    	DBManager db;


		//  전송받은 데이터 담는 리스트들.
		public List<Vector2d> ArSpace_gps_list;

		public List<string> ArSpace_name_list; 


		//

		public GameObject ArSpace_prefab;//circle
		private List<GameObject> ArSpace_object_List = new List<GameObject>();
		
		HashSet<string> AR_CUR_ID_SET = new HashSet<string>(); //AR SPACE 중복 확인에 사용.



		public GameObject Location_provider;

		bool user_in_flag=false;//for문에서 한번이라도 ar space에 접근시 true.

		private Vector2d user_position = new Vector2d();

		private int current_index = -1;//현재 위치해있는 인덱스.
		

		GameObject target_object=null;
		public GameObject user_arrow;
	
		void LoadSpace(){
			//현재 위치 주변의 공간을 불러온다.
			

			Debug.Log("Load spaces");
			db.GetNearSpaces(user_position.x, user_position.y,1000).ContinueWith((spaces) =>
            {
                if (spaces == null)
                {
                    Debug.Log("Failed to get near spaces");
                }
                else
                {
					Debug.Log("load finished");
					//불러왔으면 먼저 삭제를 해줘야한다.
					HashSet<string> new_ar_id_set = new HashSet<string>();

					for(int index = 0; index< spaces.Length; index++){
						//Debug.Log("index: "+index);
						new_ar_id_set.Add(spaces[index].id);

						if(AR_CUR_ID_SET.Add(spaces[index].id)==false){
							//이미 존재하는 경우.
							continue;
						}

						GameObject arInstance = Instantiate(ArSpace_prefab);
						arInstance.GetComponent<Circle>().id = spaces[index].id;// 굳이 스트링이어야 하나?
						arInstance.GetComponent<Circle>().Pos= new Vector2d(spaces[index].x,spaces[index].y); // gps 삽입. 
						arInstance.GetComponent<Circle>()._map=_map;
						arInstance.GetComponent<Circle>().Space_title = spaces[index].name; //  장소 명 삽입.
						ArSpace_object_List.Add(arInstance);

					}
					

					//갱신 안된 것 제거.
					AR_CUR_ID_SET.ExceptWith(new_ar_id_set);
					for (int i = ArSpace_object_List.Count - 1; i >= 0; i--)
					{
						Debug.Log("remove : "+ i.ToString());
						if (AR_CUR_ID_SET.Contains(ArSpace_object_List[i].GetComponent<Circle>().id)){
							Destroy(ArSpace_object_List[i]);
							ArSpace_object_List.RemoveAt(i);
						}
							
						//현재 문제점. 제거해도 안에 있는 객체는 안없어진다!!!


					}
					


                }
            });




	
			


		}

		void Start()
		{
			//이 부분에 firebase 추가.
        auth = AuthManager.Instance;
        db = DBManager.Instance;
        auth.Load((loaded) =>
        {
            if (!loaded)
            {
                Debug.Log("Failed to load auth manager");
            }
            else
            {
                string email = "user1@test.com";
                string password = "121212";
                auth.SignIn(email, password, (error) =>
                {
                    if (error == AuthManager.SignInError.None)
                    {
						Debug.Log("로그인 완료");
						//test();
                    }
                    else
                    {
                        Debug.Log(error);
                    }
                });
            }
        });


		
		user_position.x=Location_provider.GetComponent<MetamongLogLocationProvider>().Get_userX();
		user_position.y=Location_provider.GetComponent<MetamongLogLocationProvider>().Get_userY();
		InvokeRepeating("LoadSpace", 1f, 10f);
			


		}

        public static float GetOneMapMeterInUnityMeters(AbstractMap map, Vector3 targetPlayerPos)
        {

            // hackish way of doing the conversion, since there is no obvious way of doing it in the API
            // taking a position X meters away, converting to map coords, and then back into unity world coords.
            var playerPosGps = map.WorldToGeoPosition(targetPlayerPos);
            var playerPosInMeters = Conversions.LatLonToMeters(playerPosGps);
            var playerPosInMeters1MeterAway = playerPosInMeters + new Vector2d(1, 0); // 직접 1미터를 더해서 바꿔오기.
            var oneMeterAwayLatLon = Conversions.MetersToLatLon(playerPosInMeters1MeterAway);
            var worldPosOneMeterAway = map.GeoToWorldPosition(oneMeterAwayLatLon);
            var oneMapMeterInUnityMeters = (worldPosOneMeterAway - targetPlayerPos).magnitude;//1미터의 비율값. 
            return oneMapMeterInUnityMeters;

        }

		public GameObject get_current_space(){
			//현재 어디 스페이스에 위치해있는지 index를 출력해주는 함수.
			//arrow manager에 사용.
			return target_object;
		}

		public bool is_zoom(){
			//현재 줌 상태인지 확인. space안에 위치해있는지.
			return user_in_flag;
		}

		

		
		float targetzoom = 18f;
		float prezoom = 16f;
		private void Update()
		{
			//업데이트에서 통신이 담겨야함.
			//

			//프레임마다 유저 gps 값을 불러오고 들어왔는지 확인.
			user_position.x=Location_provider.GetComponent<MetamongLogLocationProvider>().Get_userX();
			user_position.y=Location_provider.GetComponent<MetamongLogLocationProvider>().Get_userY();
			//Debug.Log(user_position);
			

			if(user_position.x==0){
				Debug.Log("유저 좌표가 설정되지 않았습니다.");
				return;
			}

			if(user_in_flag){
				prezoom  = Mathf.Lerp(prezoom, 18f, Time.deltaTime*2);
				_map.GetComponent<AbstractMap>().SetZoom(prezoom);
				_map.GetComponent<AbstractMap>().UpdateMap();

				//화살표 나타내기
				if(!user_arrow.activeSelf){
					user_arrow.SetActive(true);
				}
				

			}else{
				prezoom  = Mathf.Lerp(prezoom, 16f, Time.deltaTime*2);
				_map.GetComponent<AbstractMap>().SetZoom(prezoom);
				_map.GetComponent<AbstractMap>().UpdateMap();
				target_object=null;

			}
			//prezoom = targetzoom;
			
			user_in_flag=false;

			//불러오고 나서 포함되는지 여부 확인
			foreach(GameObject arspace in ArSpace_object_List ){
				
				var howfar =(Conversions.LatLonToMeters(user_position) - Conversions.LatLonToMeters(arspace.GetComponent<Circle>().Pos)).magnitude;	
				if(howfar<50){
					Debug.Log("in");
					Debug.Log(howfar);
					//원의 크기로 봤을때는 반지름이 50m 넘어보인다. 어디가 문제인지.
					
					//진입 여부에 따라 다른 동작 수행.
					
					//진입했으면 zoom , 주변 나침반 on
					//16 -> 18.1
					user_in_flag=true;
					target_object=arspace;



					//1. ar space 2m 반경이면
					if(howfar<2){
						Debug.Log("Deep in");
						Debug.Log(howfar);

					}




				}
				
			}


			



		}
}
