#!/usr/bin/env python3
"""Project the production adaptive layout slots and real panel sizes; no renderer."""
from pathlib import Path
import math,re
s=(Path(__file__).resolve().parents[3]/'Assets/Script/BeerGlassPour.cs').read_text()
v=lambda t:tuple(float(x.strip().rstrip('f')) for x in t.split(','))
dot=lambda a,b:sum(x*y for x,y in zip(a,b))
mul=lambda a,t:tuple(x*t for x in a)
add=lambda a,b:tuple(x+y for x,y in zip(a,b))
def norm(a):return mul(a,1/math.sqrt(dot(a,a)))
def cross(a,b):return(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def box(name,slot,eye=(0,0,0),yaw=0,pitch=0):
 pattern=r'case SpatialSlot\.'+slot+r': return new Vector3\(([^)]+)\)'
 match=re.search(pattern,s) if slot!='Focus' else re.search(r'default: return new Vector3\(([^)]+)\)',s[s.index('internal static class SpatialLayout'):])
 pos=v(match[1]);size=re.search(name+r' = new WorldPanel\([^\n]+?, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f',s)
 w,h,units=map(float,size.groups());w=(w+18)*units;h=(h+18)*units
 normal=norm(tuple(x-y for x,y in zip(pos,eye)));right=norm(cross((0,1,0),normal));up=cross(normal,right);pixels=720/(2*math.tan(math.pi/6));points=[]
 for x in [-.5,.5]:
  for y in [-.5,.5]:
   p=tuple(a-b for a,b in zip(add(add(pos,mul(right,w*x)),mul(up,h*y)),eye))
   yy=math.radians(yaw);pp=math.radians(pitch);front=(math.sin(yy)*math.cos(pp),-math.sin(pp),math.cos(yy)*math.cos(pp));view_right=(math.cos(yy),0,-math.sin(yy));view_up=cross(front,view_right);depth=dot(p,front)
   points.append((640+dot(p,view_right)/depth*pixels,360-dot(p,view_up)/depth*pixels))
 lo=tuple(min(p[i] for p in points) for i in range(2));hi=tuple(max(p[i] for p in points) for i in range(2))
 if eye==(0,0,0) and yaw==0 and pitch==0: assert lo[0]>0 and lo[1]>0 and hi[0]<1280 and hi[1]<720,(name,'outside initial 16:9 view',lo,hi)
 return lo,hi,[points[i] for i in (0,2,3,1)]
layouts=[ [('statusBoard','Primary'),('guideBoard','Secondary')], [('decisionBoard','Primary'),('guideBoard','Secondary')], [('evaluationBoard','Focus')], [('recipeBoard','Focus')], [('workBoard','Header'),('controlsBoard','Secondary'),('feedbackBoard','Receipt')], [('clockBoard','Timer'),('feedbackBoard','Receipt')], [('orderBoard','Order'),('controlsBoard','Secondary'),('feedbackBoard','Receipt')], [('resultBoard','Focus')]]
def intersects(left,right):
 for polygon in (left,right):
  for index,a in enumerate(polygon):
   b=polygon[(index+1)%len(polygon)];axis=(a[1]-b[1],b[0]-a[0])
   la=[dot(p,axis) for p in left];ra=[dot(p,axis) for p in right]
   if max(la)<=min(ra) or max(ra)<=min(la):return False
 return True
for eye in [(0,0,0),(.4,0,0),(-.4,0,0),(0,.2,0),(.35,0,0),(-.35,0,0)]:
 for yaw,pitch in [(0,0),(25,0),(-25,0),(0,12),(0,-12)]:
  for layout in layouts:
   bounds=[box(*entry,eye,yaw,pitch) for entry in layout]
   for i,a in enumerate(bounds):
    for j,b in enumerate(bounds[i+1:],i+1):
     assert not intersects(a[2],b[2]),('overlap',layout[i],layout[j],eye,yaw,pitch)
print('PASS: active service/decision/recipe/practice/report slots, including frames, fit the reference view and avoid overlap across 30 nearby viewing poses; static projection only.')

# Practice furniture is visible even though its display colliders are disabled.
# Check real initial camera/target and world vertices, not only their screen projections.
cam=v(re.search(r'view.transform.localPosition = new Vector3\(([^)]+)\)',s)[1])
target=v(re.search(r'view.transform.LookAt\(origin \+ new Vector3\(([^)]+)\)',s)[1])
forward=norm(tuple(a-b for a,b in zip(target,cam)));right=norm(cross((0,1,0),forward));up=cross(forward,right)
worktop=re.search(r'Part\("Worktop"[^\n]+new Vector3\(([^)]+)\), new Vector3\(([^)]+)\)',s)
centre,size=v(worktop[1]),v(worktop[2]);top=centre[1]+size[1]*.5
for name,slot in [('workBoard','Header'),('controlsBoard','Secondary'),('feedbackBoard','Receipt'),('orderBoard','Order'),('clockBoard','Timer'),('resultBoard','Focus')]:
 match=re.search(r'case SpatialSlot\.'+slot+r': return new Vector3\(([^)]+)\)',s) if slot!='Focus' else re.search(r'default: return new Vector3\(([^)]+)\)',s[s.index('internal static class SpatialLayout'):])
 offset=v(match[1]);pos=add(cam,add(add(mul(right,offset[0]),mul(up,offset[1])),mul(forward,offset[2])))
 dim=re.search(name+r' = new WorldPanel\([^\n]+?, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f',s);w,h,units=map(float,dim.groups());normal=norm(tuple(a-b for a,b in zip(pos,cam)));r=norm(cross((0,1,0),normal));u=cross(normal,r)
 vertices=[add(add(pos,mul(r,(w+18)*units*x)),mul(u,(h+18)*units*y)) for x in (-.5,.5) for y in (-.5,.5)]
 assert min(p[1] for p in vertices)>top+.02,('Practice card intersects worktop height',name,min(p[1] for p in vertices),top)
print('PASS: practice guide/control/order/receipt/menu frame vertices remain above the actual worktop from the original camera pose; static geometry only.')
