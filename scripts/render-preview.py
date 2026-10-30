"""Software render actual ImGui draw commands exported by JiZhang.Preview.
Requires Pillow and numpy, development only. No game data is read.
"""
import json,struct,sys
from pathlib import Path
import numpy as np
from PIL import Image
root=Path(sys.argv[1] if len(sys.argv)>1 else 'artifacts/ui-preview')
atlases={}
for meta in root.glob('atlas-*.json'):
 d=json.loads(meta.read_text());index=int(meta.stem.split('-')[1])+1
 atlases[index]=np.frombuffer(meta.with_suffix('.rgba').read_bytes(),np.uint8).reshape(d['height'],d['width'],4)
for source in root.glob('*.bin'):
 data=memoryview(source.read_bytes());offset=0
 def read(fmt):
  global offset
  result=struct.unpack_from('<'+fmt,data,offset);offset+=struct.calcsize('<'+fmt);return result
 width,height,lists=read('iii');canvas=np.zeros((height,width,4),np.float32);canvas[:]=[.96,.965,.96,1]
 for _ in range(lists):
  nv,ni,nc=read('iii')
  vertices=np.frombuffer(data[offset:offset+nv*20],dtype=[('pos','<f4',2),('uv','<f4',2),('col','u1',4)]);offset+=nv*20
  indices=np.frombuffer(data[offset:offset+ni*2],dtype='<u2');offset+=ni*2
  for _ in range(nc):
   x1,y1,x2,y2,tex,vo,io,count=read('ffffQIII');atlas=atlases.get(tex)
   if atlas is None:continue
   for tri in indices[io:io+count].reshape(-1,3):
    verts=vertices[tri.astype(int)+vo];p=verts['pos'];uv=verts['uv'];colors=verts['col'].astype(np.float32)/255
    low=np.maximum(np.floor(p.min(axis=0)).astype(int),[max(0,int(x1)),max(0,int(y1))]);high=np.minimum(np.ceil(p.max(axis=0)).astype(int),[min(width,int(x2)),min(height,int(y2))])
    if np.any(high<=low):continue
    a,b,c=p;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(den)<1e-6:continue
    yy,xx=np.mgrid[low[1]:high[1],low[0]:high[0]];xx=xx+.5;yy=yy+.5
    w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
    w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w2=1-w0-w1
    mask=(w0>=0)&(w1>=0)&(w2>=0)
    if not mask.any():continue
    weights=np.stack([w0,w1,w2],axis=-1);coords=weights@uv
    tx=np.clip((coords[...,0]*atlas.shape[1]).astype(int),0,atlas.shape[1]-1);ty=np.clip((coords[...,1]*atlas.shape[0]).astype(int),0,atlas.shape[0]-1)
    pixel=(weights@colors)*atlas[ty,tx]/255;alpha=pixel[...,3:4]*mask[...,None]
    region=canvas[low[1]:high[1],low[0]:high[0]];region[...,:3]=pixel[...,:3]*alpha+region[...,:3]*(1-alpha)
 Image.fromarray(np.uint8(np.clip(canvas,0,1)*255)).save(source.with_suffix('.png'))
 print(source.with_suffix('.png'))
