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
 static void LabelCells(object practice){
  foreach(WorldPanel board in ((IDictionary)Get(practice,"targetBoards")).Values){
   UnityEngine.UI.Text previous=null;
   foreach(UnityEngine.UI.Graphic graphic in (IList)Get(board,"graphics")){
    if(!(graphic is UnityEngine.UI.Text text)||!text.gameObject.activeSelf||string.IsNullOrEmpty(text.text))continue;
    var cell=text.rectTransform;float top=-cell.anchoredPosition.y;
    Check(cell.anchoredPosition.x>=0&&top>=0&&cell.anchoredPosition.x+cell.sizeDelta.x<=board.Width&&top+cell.sizeDelta.y<=board.Height,"Label text stays inside its world card");
    Check(text.resizeTextForBestFit&&text.resizeTextMinSize>=18,"World text has bounded fitting for Web layouts");
    if(previous!=null)Check(-previous.rectTransform.anchoredPosition.y+previous.rectTransform.sizeDelta.y<=top,"Name and selection status occupy separate rows");
    previous=text;
   }
  }
 }
 static object Practice(BeerGlassPour owner,int recipe,bool service=false){var t=typeof(BeerGlassPour).GetNestedType("CocktailPractice",BindingFlags.NonPublic);return Activator.CreateInstance(t,BindingFlags.Public|BindingFlags.Instance,null,new object[]{owner,recipe,service},null);}
 static void PracticeUIAndRules(){
  Input.Click=false;var go=new GameObject("Player");var owner=go.AddComponent<BeerGlassPour>();var uniform=go.AddComponent<UniformInteraction>();Set(owner,"uniform",uniform);owner.playerCamera=new GameObject("Main").AddComponent<Camera>();owner.playerCamera.enabled=true;
  var passed=(int[])Get(owner,"passedTrials");
  for(int recipe=0;recipe<3;recipe++)for(int trial=0;trial<3;trial++){
   passed[recipe]=trial;Time.frameCount++;var p=Practice(owner,recipe);Check((float)p.GetType().GetProperty("RoundSeconds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p)==(trial==0?90:trial==1?60:45),"Trial timer preserved");
   Call(p,"Draw");Check(Get(p,"resultBoard")!=null,"Intro uses physical result board");Set(p,"awaitingStart",false);Time.frameCount++;Call(p,"Draw");
   Check(trial==2?Get(p,"clockBoard")!=null:Get(p,"workBoard")!=null,"Timer is integrated in the guide, or compact in the recall round");LabelCells(p);
   if(trial==0){
    Set(p,"hovered",10);Time.frameCount++;Call(p,"Draw");LabelCells(p);
    var bottles=(IDictionary)Get(p,"targets");var labels=(IDictionary)Get(p,"targetBoards");var bottle=(GameObject)bottles[10];
    var oldPosition=bottle.transform.position;var labelPosition=((GameObject)Get(labels[10],"root")).transform.position;
    bottle.transform.position=oldPosition+Vector3.up;Call(p,"Draw");
    Check(((GameObject)Get(labels[10],"root")).transform.position==labelPosition,"Pour animation does not drag the name card across other labels");bottle.transform.position=oldPosition;
   }
   if(trial==2)Check(Get(p,"workBoard")==null&&Get(p,"controlsBoard")==null&&((IDictionary)Get(p,"targetBoards")).Count==0,"Final round hides recipe and hint labels");
   Call(p,"SelectTarget",recipe);Check(!(bool)Get(p,"failed"),"Correct glass accepted");
   Check(((GameObject[])Get(p,"rings"))[recipe].activeSelf,"Selected glass remains marked in every round");
   if(trial==2){
    Check((string)Get(p,"noticeTitle")=="GLASS SELECTED","Third round acknowledges glass selection");
    Check(!((string)Get(p,"noticeDetail")).Contains("oz"),"Third-round confirmation has no quantities");Check((float)Get(p,"noticeUntil")-Time.unscaledTime>=3.19f,"Third-round acknowledgement remains readable");
    Time.frameCount++;Call(p,"Draw");Check(((WorldPanel)Get(p,"feedbackBoard")).Visible,"Third-round receipt is visible");
    Check(((IDictionary)Get(p,"targetBoards")).Count==0,"Receipt suppresses duplicate floating labels");
    Time.unscaledTime=(float)Get(p,"noticeUntil")+.01f;Time.frameCount++;Call(p,"Draw");LabelCells(p);
    Check(((IDictionary)Get(p,"targetBoards")).Count==1,"After the receipt expires, only the selected glass is labelled");
    Check((string)Call(p,"TargetStatus",recipe)=="SELECTED","Third-round selected label is explicit");
   }
   if(recipe==0){Call(p,"AddIngredient",6);Call(p,"AddIngredient",5);Call(p,"StopPour",true);Call(p,"AddIngredient",1);Call(p,"StopPour",true);}
   if(recipe==1){foreach(int ingredient in new[]{6,4,1,9}){Call(p,"AddIngredient",ingredient);Call(p,"StopPour",true);}Call(p,"SelectTarget",30);for(int i=0;i<30;i++)Call(p,"CountMotion",i%2==0?100f:-100f);Check((bool)Get(p,"stirred"),"15 stir round trips completes");}
   if(recipe==2){Call(p,"SetMeasure",.5f);foreach(int ingredient in new[]{3,2,1,0}){Call(p,"AddIngredient",ingredient);Call(p,"StopPour",true);}Call(p,"AddIngredient",6);Call(p,"SelectTarget",31);for(int i=0;i<30;i++)Call(p,"CountMotion",i%2==0?100f:-100f);Check((bool)Get(p,"shaken"),"15 shake round trips completes");Call(p,"AddIngredient",6);Call(p,"SelectTarget",31);Call(p,"StopPour",true);Call(p,"SetMeasure",1f);Call(p,"AddIngredient",8);Call(p,"StopPour",true);Call(p,"AddIngredient",7);Call(p,"StopPour",true);}
   Check(!(bool)Get(p,"failed"),"Unordered valid recipe accepted");
   if(trial==2){Check(Time.unscaledTime<(float)Get(p,"noticeUntil"),"Third-round pour/technique completion gets confirmation");Check(!((string)Get(p,"noticeDetail")).Contains("oz")&&!((string)Get(p,"noticeDetail")).Contains("Click"),"Recall feedback does not reveal next step");}
   Time.frameCount++;Call(p,"Draw");Call(p,"TryFinish");Check((bool)Get(p,"completed"),"Correct cocktail completes");Check(passed[recipe]>=trial+1,"Trial completion persists");Time.frameCount++;Call(p,"Draw");Call(p,"Dispose");
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
  foreach(int drink in new[]{0,1,2,3,4}){Set(night,"order",drink);EnumField(night,"state","Preparing");Call(night,"Draw");Check(((WorldPanel)Get(night,"statusBoard")).Visible&&!((WorldPanel)Get(night,"decisionBoard")).Visible,"The unified order card replaces duplicate tickets and decisions");}
  Call(night,"HideUI");Check(!((WorldPanel)Get(night,"statusBoard")).Visible,"Main night boards hidden during practice");Set(night,"completedGuests",10);EnumField(night,"state","Leaving");Call(night,"FinishShift");Call(night,"Draw");Check(((WorldPanel)Get(night,"evaluationBoard")).Visible,"End of shift result remains in world");Call(night,"Dispose");
  var table=new GameObject("Table").AddComponent<BoxCollider>();table.size=new Vector3(5,.1f,.95f);table.center=Vector3.zero;table.transform.position=new Vector3(40,1.2f,3.8f);var cards=Nested("RecipeCards",owner,table);Set(owner,"recipeCards",cards);
  foreach(int recipe in new[]{0,1,2})foreach(int tab in new[]{0,1}){Set(cards,"selected",recipe);Set(cards,"contentTab",tab);Call(cards,"DrawOverlay");Check(((WorldPanel)Get(cards,"recipeBoard")).Visible,"All recipe tabs render on world reading stand");}
  Call(cards,"Close");Check(!((WorldPanel)Get(cards,"recipeBoard")).Visible,"Closed card releases world panel");Set(uniform,"shiftStarted",true);Set(owner,"night",null);Call(owner,"DrawWorldUI");Check(((WorldPanel)Get(owner,"beerBoard")).Visible,"Beer status uses a world placard");var served=(bool[])Get(owner,"served");served[0]=served[1]=true;Call(owner,"DrawWorldUI");Check(((WorldPanel)Get(owner,"cocktailBoard")).Visible,"Training progress uses world checklist");
  Call(cards,"Dispose");Call(owner,"OnDestroy");
 }
 static void PointerVisibility(){
  var cam=new GameObject("PointerCamera").AddComponent<Camera>();cam.enabled=true;cam.transform.position=new Vector3(0,1.7f,0);Cursor.lockState=CursorLockMode.Locked;Input.Click=false;
  var owner=new GameObject("PointerOwner");var panel=Board(owner,cam,"PointerTarget",2);Time.frameCount++;panel.Begin();panel.Button(new Rect(0,0,400,200),"Target");panel.End();
  var pointer=new WorldAim();pointer.Tick(cam,true);var point=(GameObject)Get(pointer,"point");
  Check(point.activeSelf&&point.transform.localScale.x>=.028f,"Pointer is larger than the old 9mm dot");
  Check(Get(pointer,"outline")!=null,"Pointer has a contrast outline");
  Check(point.transform.parent==null,"Pointer stays in world space");
  var initialScale=point.transform.localScale.x;Input.Click=true;pointer.Tick(cam,true);Check(point.transform.localScale.x>initialScale,"Interactive click visibly pulses pointer");
  panel.Hide();pointer.Tick(cam);Check(!point.activeSelf,"Pointer hidden without a surface hit");pointer.Dispose();panel.Dispose();Input.Click=false;
 }
 static object Property(object o,string name)=>o.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static void MouseDelta(float x,float y,bool held=false){
  Input.Click=false;Input.AxisX=x/18f;Input.AxisY=y/18f;Input.MouseHeld=held;
  UnityEngine.InputSystem.Mouse.current??=new UnityEngine.InputSystem.Mouse();
  UnityEngine.InputSystem.Mouse.current.delta.value=new Vector2(x,y);
  UnityEngine.InputSystem.Mouse.current.leftButton.isPressed=held;
 }
 static void FreeLookAndWorldSigns(){
  var go=new GameObject("LookPlayer");var owner=go.AddComponent<BeerGlassPour>();Set(owner,"uniform",go.AddComponent<UniformInteraction>());owner.playerCamera=new GameObject("MainCamera").AddComponent<Camera>();
  Application.isFocused=true;MouseDelta(0,0);var practice=Practice(owner,0);var cam=(Camera)Property(practice,"ViewCamera");
  Check(Cursor.lockState==CursorLockMode.Locked,"Practice begins with first-person pointer capture");
  float yaw=(float)Get(practice,"lookYaw"),pitch=(float)Get(practice,"lookPitch"),remaining=(float)Get(practice,"remaining");var forward=cam.transform.forward;
  MouseDelta(20,10);Call(practice,"Tick");
  Check((float)Get(practice,"lookYaw")>yaw&&(float)Get(practice,"lookPitch")<pitch,"Mouse moves camera with no right button, including briefing");
  Check((cam.transform.forward-forward).sqrMagnitude>.0001f,"Look delta rotates the camera, not a UI transform");
  Check((float)Get(practice,"remaining")==remaining,"Briefing look does not start the timer");
  Set(practice,"awaitingStart",false);MouseDelta(0,0);Time.frameCount++;Call(practice,"Draw");
  var panel=(WorldPanel)Get(practice,"workBoard");var boardRoot=(GameObject)Get(panel,"root");var position=boardRoot.transform.position;var orientation=boardRoot.transform.rotation;
  MouseDelta(25,5);Call(practice,"Tick");Time.frameCount++;Call(practice,"Draw");
  Check(boardRoot.transform.position==position&&Math.Abs(boardRoot.transform.rotation.y-orientation.y)<.0001f,"Small look changes do not immediately relocate the spatial card");
  var pointer=(Vector2)Call(practice,"Pointer");Check(pointer.x==Screen.width*.5f&&pointer.y==Screen.height*.5f,"Selection ray uses the centre, not a free OS cursor");
  Cursor.lockState=CursorLockMode.None;Input.mousePosition=new Vector3(10,10,0);UnityEngine.InputSystem.Mouse.current.position.value=new Vector2(10,10);
  Check(Vector3.Dot(SpatialInput.Ray(cam).direction,cam.transform.forward)>.999f,"Uncaptured Web pointer still uses the practice gaze ray");Cursor.lockState=CursorLockMode.Locked;
  Set(practice,"toolHeld",true);MouseDelta(50,40,true);yaw=(float)Get(practice,"lookYaw");Call(practice,"LookAround");Check((float)Get(practice,"lookYaw")==yaw,"Technique drag owns mouse delta without moving view");
  Set(practice,"toolHeld",false);Set(practice,"gestureReleaseRequired",true);Call(practice,"LookAround");Check((float)Get(practice,"lookYaw")==yaw,"Completed gesture cannot kick the camera while button stays held");
  MouseDelta(20,0,false);Call(practice,"LookAround");Check((float)Get(practice,"lookYaw")>yaw,"Releasing the tool gesture resumes ordinary mouse look");
  Application.isFocused=false;yaw=(float)Get(practice,"lookYaw");Call(practice,"LookAround");Check((float)Get(practice,"lookYaw")==yaw,"Background window cannot rotate camera");Application.isFocused=true;
  MouseDelta(0,0);Call(practice,"SelectTarget",0);Call(practice,"AddIngredient",1);remaining=(float)Get(practice,"remaining");yaw=(float)Get(practice,"lookYaw");MouseDelta(15,0);Call(practice,"Tick");
  Check((float)Get(practice,"lookYaw")>yaw&&(float)Get(practice,"pourTime")>0,"View remains free while a drink pours");
  Check((float)Get(practice,"remaining")<remaining,"Mouse look never pauses an active trial timer");Call(practice,"Dispose");MouseDelta(0,0);
  var viewer=new GameObject("Viewer").AddComponent<Camera>();viewer.transform.position=new Vector3(0,1.7f,0);var fixedPanel=new WorldPanel(go,"PhysicalSign",400,200);var anchor=new Vector3(0,1.7f,2);
  fixedPanel.PoseFixed(anchor,Quaternion.identity,viewer);fixedPanel.Begin();fixedPanel.Text(new Rect(10,10,380,180),"IN THE WORLD");fixedPanel.End();
  viewer.transform.position=new Vector3(1,1.7f,0);viewer.transform.rotation=Quaternion.LookRotation(Vector3.right);fixedPanel.PoseFixed(anchor,Quaternion.identity,viewer);var fixedRoot=(GameObject)Get(fixedPanel,"root");
  Check(fixedRoot.transform.position==anchor&&fixedRoot.transform.rotation.y==0&&fixedRoot.transform.parent==null,"Physical sign cannot follow head movement or viewer translation");
  Check(!WorldPanel.TryPointerHit(viewer,out var unused),"Turning away leaves the placard outside the gaze ray");fixedPanel.Dispose();
 }
 static object ServiceSession(out BeerGlassPour owner){
  var go=new GameObject("ShiftPlayer");owner=go.AddComponent<BeerGlassPour>();owner.playerCamera=new GameObject("MainCamera").AddComponent<Camera>();Set(owner,"uniform",go.AddComponent<UniformInteraction>());
  var desk=new GameObject("Counter").AddComponent<BoxCollider>();desk.bounds=new Bounds(new Vector3(40,1,1.6f),new Vector3(10,.15f,1.3f));Set(owner,"serviceDesk",desk);
  var marker=new GameObject("Serve");marker.transform.position=new Vector3(43,1.09f,1.27f);Set(owner,"serviceMarker",marker);
  var session=Nested("NightService",owner);Set(owner,"night",session);return session;
 }
 static void ShiftEvaluationScenarios(){
  // S=served; A=correct age refusal; I=correct intoxication refusal; F=failed drink; D=wrong refusal; U=unsafe sale choice.
  string[] sessions={"SSSSSSSSAI","SSSSSSSSSD","SSSSSSSSDD","SSSSSSADDD","SSSSSSFFFF","SSSSSSSSSU"};
  int[] scores={100,90,80,70,60,90};string[] grades={"A","A","B","C","D","D"};
  for(int test=0;test<sessions.Length;test++){
   var session=ServiceSession(out var owner);Call(session,"Draw");var summary=(WorldPanel)Get(session,"statusBoard");var summaryRoot=(GameObject)Get(summary,"root");
   Check(summaryRoot.transform.parent==null&&summaryRoot.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"Open announcement is a single unparented world card");
   Set(session,"completedGuests",9);Call(session,"FinishShift");Check(Get(session,"evaluation")==null,"Report cannot finish before 10 customers");
   int finished=0;
   foreach(char action in sessions[test]){
    EnumField(session,"state","Screening");EnumField(session,"kind",action=='A'||action=='U'?"CheckID":action=='I'?"Intoxicated":"Regular");Set(session,"order",action=='F'?2:0);Set(session,"idChecked",false);
    if(action=='A'){Call(session,"RefuseService",true);Call(session,"RefuseService",true);}
    else if(action=='I'||action=='D')Call(session,"RefuseService",false);
    else {Call(session,"AcceptOrder");if(action=='S')Call(session,"CompleteBeer");else if(action=='F')Call(session,"CompleteCocktail",false);}
    finished++;Set(session,"completedGuests",finished);
   }
   // The last guest exits through the real NightService.Tick completion path.
   var lastGuest=new GameObject("LastGuest");lastGuest.transform.position=(Vector3)Get(session,"entrance");Set(session,"guest",lastGuest);Set(session,"completedGuests",9);Set(session,"atAisle",true);Set(session,"deadline",0f);EnumField(session,"state","Leaving");Time.unscaledTime=1f;
   Call(session,"Tick");var result=Get(session,"evaluation");Check(result!=null,"The tenth guest exiting produces the evaluation through Tick");
   Check((int)Property(result,"Score")==scores[test],"Score comes from successful serving and correct refusals");Check((string)Property(result,"Grade")==grades[test],"Grade follows accuracy and safety override");
   Check((bool)Property(result,"Cleared")== (test<4),"70+ accuracy clears only with no unsafe sale choices");
   Check((int)Get(session,"servedCount")+(int)Get(session,"refusedCount")+(int)Get(session,"missedCount")==10,"Each encounter contributes exactly one outcome");
   if(test==0)Check((int)Get(session,"ageRefusals")==1&&(int)Get(session,"intoxicationRefusals")==1&&(int)Get(session,"idChecks")==1,"ID and intoxication decisions are tracked separately");
   if(test==4)Check((int)Get(session,"failedDrinkOrders")==4&&(int)Get(session,"decisionErrors")==0,"Drink failures remain separate from decision errors");
   if(test==5)Check((int)Get(session,"unsafeSaleChoices")==1&&(int)Get(session,"decisionErrors")==1,"An unsafe choice is recorded once and blocks clearance");
   Call(session,"Draw");var report=(WorldPanel)Get(session,"evaluationBoard");var reportRoot=(GameObject)Get(report,"root");Check(report.Visible&&reportRoot.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace&&reportRoot.transform.parent==null,"Final scorecard is a persistent world object");
   var reportPosition=reportRoot.transform.position;owner.playerCamera.transform.position+=Vector3.right;Call(session,"Draw");Check(reportRoot.transform.position==reportPosition,"Final evaluation does not jump immediately during a small movement");
   int frozen=(int)Property(result,"Score");Set(session,"servedCount",0);Call(session,"FinishShift");Check(ReferenceEquals(result,Get(session,"evaluation"))&&(int)Property(result,"Score")==frozen,"Report is snapshotted once and cannot double-count");
   Call(session,"HideUI");Check(!summary.Visible&&!report.Visible,"Night UI hides cleanly for owner suspension");Call(session,"Dispose");
  }
 }
 static void Advance(float seconds){Time.unscaledTime+=seconds;Time.unscaledDeltaTime=seconds;Time.deltaTime=seconds;Time.frameCount++;}
 static void AdaptiveLayoutBehavior(){
  Input.Click=false;MouseDelta(0,0);var owner=new GameObject("AdaptiveOwner");var cam=new GameObject("AdaptiveCamera").AddComponent<Camera>();cam.fieldOfView=60;cam.enabled=true;cam.transform.position=new Vector3(0,1.7f,0);Cursor.lockState=CursorLockMode.Locked;
  SpatialLayout.BeginContext(cam,"adaptive-test");var panel=new WorldPanel(owner,"AdaptiveCard",740,400,.0019f);panel.PoseAdaptive(cam,SpatialSlot.Primary);panel.Begin();panel.Button(new Rect(10,10,720,380),"ACT");panel.End();var root=(GameObject)Get(panel,"root");var start=root.transform.position;
  cam.transform.rotation=Quaternion.Euler(0,100,0);Advance(.05f);SpatialLayout.BeginContext(cam,"adaptive-test");panel.PoseAdaptive(cam,SpatialSlot.Primary);Check(root.transform.position==start,"Brief head turns do not snap or head-lock the UI");
  Advance(.20f);SpatialLayout.BeginContext(cam,"adaptive-test");panel.PoseAdaptive(cam,SpatialSlot.Primary);Check(root.transform.position==start,"Recentring waits for the comfort dwell");
  Advance(.25f);SpatialLayout.BeginContext(cam,"adaptive-test");panel.PoseAdaptive(cam,SpatialSlot.Primary);var direction=root.transform.position-cam.transform.position;
  Check(Vector3.Angle(start-cam.transform.position,direction)>0&&Vector3.Angle(start-cam.transform.position,direction)<7,"Long turns start a bounded smooth world-space catch-up");
  for(int i=0;i<150;i++){Advance(.016f);SpatialLayout.BeginContext(cam,"adaptive-test");panel.PoseAdaptive(cam,SpatialSlot.Primary);}
  Check(Vector3.Angle(root.transform.position-cam.transform.position,cam.transform.forward)<35,"Guide returns beside the new viewing direction");
  panel.Begin();panel.Text(new Rect(10,10,720,380),"READING ONLY");panel.End();
  cam.transform.position+=Vector3.right*.3f;cam.transform.rotation=Quaternion.LookRotation(root.transform.position-cam.transform.position);var held=root.transform.position;
  Check(panel.ContainsPointer(cam),"Interactive card can be aimed at in 3D");
  for(int i=0;i<60;i++){Advance(.016f);SpatialLayout.BeginContext(cam,"adaptive-test");panel.PoseAdaptive(cam,SpatialSlot.Primary);}
  Check(root.transform.position==held,"A pointed-at button stays stable during reading and small position changes");
  var layoutFrames=(IDictionary)typeof(SpatialLayout).GetField("frames",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);var headingBefore=((Quaternion)Get(layoutFrames[cam],"heading"))*Vector3.forward;
  cam.transform.position+=Vector3.right;cam.transform.rotation=Quaternion.LookRotation(root.transform.position-cam.transform.position);
  for(int i=0;i<30;i++){Advance(.016f);SpatialLayout.BeginContext(cam,"adaptive-test");panel.PoseAdaptive(cam,SpatialSlot.Primary);cam.transform.rotation=Quaternion.LookRotation(root.transform.position-cam.transform.position);}
  Check(Vector3.Distance(root.transform.position,held)>.1f,"Sustained walking carries the group along instead of passing through it");
  Check(Vector3.Angle(headingBefore,((Quaternion)Get(layoutFrames[cam],"heading"))*Vector3.forward)<.01f,"Reading a non-button card holds its layout angle during walking");
  var secondary=new WorldPanel(owner,"RightCard",340,410,.00165f);secondary.PoseAdaptive(cam,SpatialSlot.Secondary);secondary.Begin();secondary.Text(new Rect(10,10,320,390),"NOTE");secondary.End();
  Check(root.transform.parent==null&&((GameObject)Get(secondary,"root")).transform.parent==null,"Adaptive cards remain world objects, never camera children");
  Check(root.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"Reactivity does not introduce screen-space UI");
  secondary.Hide();panel.Hide();var wall=new GameObject("Wall").AddComponent<BoxCollider>();wall.bounds=new Bounds(Vector3.forward*1.2f,new Vector3(4,3,.2f));Physics.TestSurface=wall;Physics.TestDistance=1.2f;
  Advance(.016f);SpatialLayout.BeginContext(cam,"near-wall");panel.PoseAdaptive(cam,SpatialSlot.Primary);
  Check(root.transform.localScale.x<.0019f,"Near scene surfaces reduce the whole layout together");
  Check(Vector3.Distance(root.transform.position,cam.transform.position)<1.2f,"Repositioned card stays in front of a nearby scene surface");
  Physics.TestSurface=null;
  cam.transform.position=new Vector3(0,1.8f,-1.6f);cam.transform.rotation=Quaternion.Euler(35,0,0);
  SpatialLayout.SetPracticeGeometry(cam,new Bounds(new Vector3(0,.49f,.8f),new Vector3(4.6f,.98f,1.9f)),.03f);Advance(.016f);SpatialLayout.BeginContext(cam,"render-furniture");panel.PoseAdaptive(cam,SpatialSlot.Primary);
  Check(root.transform.localScale.x<.0019f,"Rendered furniture limits card placement even with disabled colliders");
  cam.transform.rotation=Quaternion.Euler(65,0,0);Advance(.016f);SpatialLayout.BeginContext(cam,"render-floor");panel.PoseAdaptive(cam,SpatialSlot.Primary);
  Check(root.transform.position.y>.05f,"Looking down keeps the spatial card above the rendered floor");
  panel.Dispose();secondary.Dispose();SpatialLayout.Release(cam);Time.unscaledDeltaTime=Time.deltaTime=.016f;MouseDelta(0,0);
 }
 static int VisibleNightCards(object night){int count=0;foreach(string field in new[]{"statusBoard","decisionBoard","guideBoard","evaluationBoard"})if(Get(night,field) is WorldPanel panel&&panel.Visible)count++;return count;}
 static void PresentationPriorities(){
  var night=ServiceSession(out var owner);Advance(.016f);Call(night,"Draw");Check(VisibleNightCards(night)==1,"Opening shows one unified guide, not an OPEN sign plus status and ticket");
  Call(night,"SpawnGuest");EnumField(night,"state","Screening");EnumField(night,"kind","CheckID");Set(night,"easyGuideVisible",true);Set(night,"order",0);Call(night,"OpenDialog");Advance(.016f);Call(night,"Draw");
  Check(VisibleNightCards(night)==2&&!((WorldPanel)Get(night,"statusBoard")).Visible,"Decision and separate easy note replace all background summaries");
  Call(night,"CloseDialog");EnumField(night,"state","Preparing");Advance(.016f);Call(night,"Draw");Check(VisibleNightCards(night)==1&&!((WorldPanel)Get(night,"guideBoard")).Visible,"Order preparation has one guide with the order inside");
  EnumField(night,"state","Leaving");Set(night,"completedGuests",10);Call(night,"FinishShift");Advance(.016f);Call(night,"Draw");Check(VisibleNightCards(night)==1&&((WorldPanel)Get(night,"evaluationBoard")).Visible,"Completed shift shows only the evaluation card");Call(night,"Dispose");
 }
 static int PracticeCards(object practice){int count=0;foreach(string field in new[]{"workBoard","controlsBoard","clockBoard","resultBoard","feedbackBoard","orderBoard"})if(Get(practice,field) is WorldPanel p&&p.Visible)count++;return count;}
 static void LivePresentation(){
  MouseDelta(0,0);var night=ServiceSession(out var owner);EnumField(night,"state","Preparing");Set(night,"order",2);Advance(.016f);Call(night,"Draw");
  var practice=Practice(owner,0,true);Set(owner,"practice",practice);Advance(.016f);Call(owner,"DrawWorldUI");
  Check(VisibleNightCards(night)==0,"Cocktail preparation hides the service overlay before opening workbench cards");
  Check(Get(practice,"workBoard")==null&&Get(practice,"clockBoard")==null,"Live order note contains its timer instead of duplicate header and clock cards");
  Check(PracticeCards(practice)==2,"Live play starts with only order/timer and controls");
  var ticket=(WorldPanel)Get(practice,"orderBoard");bool timer=false;
  foreach(UnityEngine.UI.Graphic graphic in (IList)Get(ticket,"graphics"))if(graphic is UnityEngine.UI.Text label&&label.text=="45.0 s")timer=true;
  Check(timer,"45-second customer timer remains visible in the order note");
  Call(practice,"SelectTarget",0);Advance(.016f);Call(owner,"DrawWorldUI");Check(PracticeCards(practice)==3,"Selection adds one temporary receipt without background duplicates");
  foreach(WorldPanel label in ((IDictionary)Get(practice,"targetBoards")).Values)Check(!label.Visible,"Receipt suppresses nearby floating nameplates");
  Call(practice,"Dispose");Call(night,"Dispose");Set(owner,"night",null);Call(owner,"OnDestroy");
 }
 public static void Main(){SpatialButtons();PointerVisibility();PracticeUIAndRules();StationAndCustomerBoards();FreeLookAndWorldSigns();ShiftEvaluationScenarios();AdaptiveLayoutBehavior();PresentationPriorities();LivePresentation();Console.WriteLine("PASS "+checks+" spatial input, UI state and recipe checks with Unity API shims; no Unity rendering or physics executed.");}
}
