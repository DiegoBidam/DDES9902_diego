#!/usr/bin/env python3
"""Read runtime UI positions from the production script; static perspective check, no renderer."""
from pathlib import Path
import math, re
source = (Path(__file__).resolve().parents[3]/'Assets/Script/BeerGlassPour.cs').read_text()
add = lambda a,b: tuple(x+y for x,y in zip(a,b))
sub = lambda a,b: tuple(x-y for x,y in zip(a,b))
mul = lambda a,t: tuple(x*t for x in a)
dot = lambda a,b: sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def normal(a): return mul(a,1/math.sqrt(dot(a,a)))
def vector(text): return tuple(float(x.strip().rstrip('f')) for x in text.split(','))
cam = vector(re.search(r'view.transform.localPosition = new Vector3\(([^)]+)\)',source)[1])
target = vector(re.search(r'view.transform.LookAt\(origin \+ new Vector3\(([^)]+)\)',source)[1])
forward = normal(sub(target,cam)); right = (1.,0.,0.); up = cross(forward,right)
fov = float(re.search(r'camera.fieldOfView = ([\d.]+)f',source)[1])
scale = 720/(2*math.tan(math.radians(fov/2)))
def box(position,width,height):
    away = sub(position,cam); facing = normal((away[0],0.,away[2])); horizontal = cross((0.,1.,0.),facing)
    points = []
    for x in [-.5,.5]:
        for y in [-.5,.5]:
            ray = sub(add(add(position,mul(horizontal,width*x)),(0.,height*y,0.)),cam)
            depth = dot(ray,forward); assert depth > 0
            points.append((640+dot(ray,right)/depth*scale,360-dot(ray,up)/depth*scale))
    return tuple(min(p[i] for p in points) for i in range(2)), tuple(max(p[i] for p in points) for i in range(2))
def overlap(a,b): return all(max(a[0][i],b[0][i]) < min(a[1][i],b[1][i]) for i in range(2))
boards = {}
for name in ['clockBoard','controlsBoard','workBoard','feedbackBoard']:
    position = vector(re.search(name+r'.Pose\(origin \+ new Vector3\(([^)]+)\)',source)[1])
    size = re.search(name+r' = new WorldPanel\(root, "[^"]+", ([\d.]+)f, ([\d.]+)f, ([\d.]+)f',source)
    width,height,units = map(float,size.groups()); boards[name] = box(position,(width+18)*units,(height+18)*units)
# Read the ingredient row and its label dimensions; offsets follow the runtime branch for ice/bottles.
row = re.search(r'bottle.transform.localPosition = new Vector3\(([-\d.]+)f \+ index \* ([\d.]+)f, TableY \+ ([\d.]+)f, ([\d.]+)f\)',source)
start,step,lift,z = map(float,row.groups()); table_y = float(re.search(r'const float TableY = ([\d.]+)f',source)[1])
offsets = re.search(r'code == 16 \? ([\d.]+)f : ([\d.]+)f',source)
ice_offset,bottle_offset = map(float,offsets.groups())
label_size = re.search(r'"Practice_ObjectLabel_" \+ code, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f',source)
w,h,units = map(float,label_size.groups())
labels = [box((start+i*step,table_y+lift+(ice_offset if i==6 else bottle_offset),z),(w+18)*units,(h+18)*units) for i in range(10)]
for name,rectangle in boards.items():
    assert all(rectangle[0][i] > 0 and rectangle[1][i] < [1280,720][i] for i in range(2)), ('Outside initial viewport',name)
    assert not any(overlap(rectangle,label) for label in labels), ('Ingredient label overlap',name)
for index,left in enumerate(labels):
    assert not any(overlap(left,right) for right in labels[index+1:]), ('Ingredient labels overlap each other',index)
assert not overlap(boards['workBoard'],boards['feedbackBoard'])
assert not overlap(boards['controlsBoard'],boards['workBoard'])
print('PASS: production UI coordinates project inside the initial 16:9 / 60-degree view and clear all 10 ingredient labels; static math only.')
