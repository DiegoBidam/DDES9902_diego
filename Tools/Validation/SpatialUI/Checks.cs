using System; using System.Reflection;using System.Collections;using UnityEngine;
static class Validation {
 static int checks;
 static void Check(bool p,string s){if(!p)throw new Exception(s);checks++;}
 static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).Invoke(o,args);
 static WorldPanel Board(GameObject owner,Camera cam,string name,float z){var p=new WorldPanel(owner,name,400,200);p.Pose(new Vector3(0,1.7f,z),cam);return p;}
 static void SpatialButtons(){
  var owner=new GameObject("Owner");var cam=new GameObject("Camera").AddComponent<Camera>();cam.transform.position=new Vector3(0,1.7f,0);Cursor.lockState=CursorLockMode.Locked;Input.Click=true;
  var rect=new Rect(0,0,400,200);Time.frameCount=1;
  var board=Board(owner,cam,"Sign",2);board.Begin();Check(!board.Button(rect,"Start"),"Opening click must not activate new board");board.End();
  Check(((GameObject)Get(board,"root")).GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"WorldSpace canvas");
  Check(((GameObject)Get(board,"root")).transform.parent==null,"No camera/player parent");
  Time.frameCount++;board.Begin();Check(board.Button(rect,"Go"),"Ray hits world button");Check(!board.Button(rect,"Double"),"One action per frame");board.End();
  int pool=((IList)Get(board,"graphics")).Count;Time.frameCount++;board.Begin();board.Button(rect,"Same");board.End();Check(((IList)Get(board,"graphics")).Count==pool,"Graphic pool reused");
  Time.frameCount++;Physics.Blocked=true;board.Begin();Check(!board.Button(rect,"Blocked"),"Scene occlusion blocks click");board.End();Physics.Blocked=false;
  var near=Board(owner,cam,"Near sign",1);near.Begin();near.Text(rect,"Solid notice");near.End();Time.frameCount++;board.Begin();Check(!board.Button(rect,"Behind notice"),"Near noninteractive board occludes button");board.End();Check(WorldPanel.TryPointerHit(cam,out var tip)&&Math.Abs(tip.z-1)<.001,"Ray tip lies on nearest actual board");near.Dispose();
  var position=((GameObject)Get(board,"root")).transform.position;cam.transform.rotation=Quaternion.LookRotation(Vector3.right);board.Pose(position,cam);Time.frameCount++;board.Begin();Check(!board.Button(rect,"Look away"),"Turning head misses fixed board");board.End();Check(((GameObject)Get(board,"root")).transform.position==position,"Head turn does not move board");
  cam.transform.rotation=Quaternion.identity;Time.frameCount++;board.Begin();board.Button(rect,"Disabled",false);board.End();Check(!WorldPanel.PointerOverInteractive(cam),"Disabled labels do not suppress stir/shake");
  board.Hide();Check(!WorldPanel.TryPointerHit(cam,out tip),"Hidden board does not capture rays");board.Dispose();
  var far=Board(owner,cam,"Far",7);far.Begin();far.Button(rect,"Too far");far.End();Time.frameCount++;Check(!WorldPanel.PointerOverInteractive(cam),"Interaction distance limited");far.Dispose();
 }
 static object Practice(BeerGlassPour owner,int recipe,bool service=false){var t=typeof(BeerGlassPour).GetNestedType("CocktailPractice",BindingFlags.NonPublic);return Activator.CreateInstance(t,BindingFlags.Public|BindingFlags.Instance,null,new object[]{owner,recipe,service},null);}
 static void PracticeUIAndRules(){
  Input.Click=false;var go=new GameObject("Player");var owner=go.AddComponent<BeerGlassPour>();var uniform=go.AddComponent<UniformInteraction>();Set(owner,"uniform",uniform);owner.playerCamera=new GameObject("Main").AddComponent<Camera>();owner.playerCamera.enabled=true;
  var passed=(int[])Get(owner,"passedTrials");
  for(int recipe=0;recipe<3;recipe++)for(int trial=0;trial<3;trial++){
   passed[recipe]=trial;Time.frameCount++;var p=Practice(owner,recipe);Check((float)p.GetType().GetProperty("RoundSeconds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p)==(trial==0?90:trial==1?60:45),"Trial timer preserved");
   Call(p,"Draw");Check(Get(p,"resultBoard")!=null,"Intro uses physical result board");Set(p,"awaitingStart",false);Time.frameCount++;Call(p,"Draw");
   Check(Get(p,"clockBoard")!=null,"Physical timer exists in all rounds");
   if(trial==2)Check(Get(p,"workBoard")==null&&Get(p,"controlsBoard")==null&&((IDictionary)Get(p,"targetBoards")).Count==0,"Final round hides recipe and hint labels");
   Call(p,"SelectTarget",recipe);Check(!(bool)Get(p,"failed"),"Correct glass accepted");
   if(recipe==0){Call(p,"AddIngredient",6);Call(p,"AddIngredient",5);Call(p,"StopPour",true);Call(p,"AddIngredient",1);Call(p,"StopPour",true);}
   if(recipe==1){foreach(int ingredient in new[]{6,4,1,9}){Call(p,"AddIngredient",ingredient);Call(p,"StopPour",true);}Call(p,"SelectTarget",30);for(int i=0;i<30;i++)Call(p,"CountMotion",i%2==0?100f:-100f);Check((bool)Get(p,"stirred"),"15 stir round trips completes");}
   if(recipe==2){Call(p,"SetMeasure",.5f);foreach(int ingredient in new[]{3,2,1,0}){Call(p,"AddIngredient",ingredient);Call(p,"StopPour",true);}Call(p,"AddIngredient",6);Call(p,"SelectTarget",31);for(int i=0;i<30;i++)Call(p,"CountMotion",i%2==0?100f:-100f);Check((bool)Get(p,"shaken"),"15 shake round trips completes");Call(p,"AddIngredient",6);Call(p,"SelectTarget",31);Call(p,"StopPour",true);Call(p,"SetMeasure",1f);Call(p,"AddIngredient",8);Call(p,"StopPour",true);Call(p,"AddIngredient",7);Call(p,"StopPour",true);}
   Check(!(bool)Get(p,"failed"),"Unordered valid recipe accepted");Time.frameCount++;Call(p,"Draw");Call(p,"TryFinish");Check((bool)Get(p,"completed"),"Correct cocktail completes");Check(passed[recipe]>=trial+1,"Trial completion persists");Time.frameCount++;Call(p,"Draw");Call(p,"Dispose");
  }
  var bad=Practice(owner,0);Set(bad,"awaitingStart",false);Call(bad,"SelectTarget",0);Call(bad,"AddIngredient",4);Check((bool)Get(bad,"failed"),"Wrong recipe still fails");Call(bad,"Draw");Call(bad,"Dispose");
  var order=Practice(owner,1,true);Check((float)order.GetType().GetProperty("RoundSeconds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(order)==45,"Live order remains 45 seconds");Call(order,"Draw");Set(order,"remaining",0f);Call(order,"Tick");Check((bool)Get(order,"failed"),"Live order timeout still fails");Call(order,"Draw");Call(order,"Dispose");
 }
 static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(BeerGlassPour).GetNestedType(name,BindingFlags.NonPublic),BindingFlags.Public|BindingFlags.Instance,null,args,null);
 static void EnumField(object o,string field,string value){var f=o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance);f.SetValue(o,Enum.Parse(f.FieldType,value));}
 static void StationAndCustomerBoards(){
  Input.Click=false;var go=new GameObject("Player");var owner=go.AddComponent<BeerGlassPour>();var uniform=go.AddComponent<UniformInteraction>();Set(owner,"uniform",uniform);owner.playerCamera=new GameObject("Main").AddComponent<Camera>();owner.playerCamera.enabled=true;
  var desk=new GameObject("Counter").AddComponent<BoxCollider>();desk.center=new Vector3(40,.5f,1.6f);desk.size=new Vector3(10,1,1.3f);desk.bounds=new Bounds(desk.center,desk.size);Set(owner,"serviceDesk",desk);var marker=new GameObject("Marker");marker.transform.position=new Vector3(43,1.09f,1.27f);Set(owner,"serviceMarker",marker);owner.beerNozzle=new GameObject("Tap").transform;owner.beerNozzle.position=new Vector3(44,1.6f,1.8f);
  var night=Nested("NightService",owner);Set(owner,"night",night);Call(night,"Draw");Check(Get(night,"statusBoard")!=null,"Night shift uses world status board");Call(night,"SpawnGuest");
  foreach(string kind in new[]{"Regular","CheckID","Intoxicated"}){
   EnumField(night,"kind",kind);EnumField(night,"state","Screening");Set(night,"easyGuideVisible",kind!="Regular");Set(night,"order",0);Call(night,"OpenDialog");Call(night,"Draw");Check(Get(night,"decisionBoard")!=null,"Customer choices use world board");Check(Cursor.lockState==CursorLockMode.Locked,"Customer dialogue preserves look input");if(kind!="Regular")Check(((WorldPanel)Get(night,"guideBoard")).Visible,"Easy guide is a visible world note");Call(night,"CloseDialog");
  }
  foreach(int drink in new[]{0,1,2,3,4}){Set(night,"order",drink);EnumField(night,"state","Preparing");Call(night,"Draw");Check(((WorldPanel)Get(night,"ticketBoard")).Visible,"World ticket persists during each order");}
  Call(night,"HideUI");Check(!((WorldPanel)Get(night,"statusBoard")).Visible,"Main night boards hidden during practice");EnumField(night,"state","Completed");Call(night,"Draw");Check(((WorldPanel)Get(night,"statusBoard")).Visible,"End of shift result remains in world");Call(night,"Dispose");
  var table=new GameObject("Table").AddComponent<BoxCollider>();table.size=new Vector3(5,.1f,.95f);table.center=Vector3.zero;table.transform.position=new Vector3(40,1.2f,3.8f);var cards=Nested("RecipeCards",owner,table);Set(owner,"recipeCards",cards);
  foreach(int recipe in new[]{0,1,2})foreach(int tab in new[]{0,1}){Set(cards,"selected",recipe);Set(cards,"contentTab",tab);Call(cards,"DrawOverlay");Check(((WorldPanel)Get(cards,"recipeBoard")).Visible,"All recipe tabs render on world reading stand");}
  Call(cards,"Close");Check(!((WorldPanel)Get(cards,"recipeBoard")).Visible,"Closed card releases world panel");Set(uniform,"shiftStarted",true);Set(owner,"night",null);Call(owner,"DrawWorldUI");Check(((WorldPanel)Get(owner,"beerBoard")).Visible,"Beer status uses a world placard");var served=(bool[])Get(owner,"served");served[0]=served[1]=true;Call(owner,"DrawWorldUI");Check(((WorldPanel)Get(owner,"cocktailBoard")).Visible,"Training progress uses world checklist");
  Call(cards,"Dispose");Call(owner,"OnDestroy");
 }
 public static void Main(){SpatialButtons();PracticeUIAndRules();StationAndCustomerBoards();Console.WriteLine("PASS "+checks+" spatial input, UI state and recipe checks with Unity API shims; no Unity rendering or physics executed.");}
}
