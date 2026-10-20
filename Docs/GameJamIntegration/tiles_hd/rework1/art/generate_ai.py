"""Actual local SDXL texture generation, logs prompt/history and never touches Unity."""
from pathlib import Path
import json,time,urllib.request
A=Path(__file__).resolve().parent
base='http://127.0.0.1:8188'
def req(path,data=None):
 r=urllib.request.Request(base+path,data=None if data is None else json.dumps(data).encode(),headers={'Content-Type':'application/json'})
 return json.load(urllib.request.urlopen(r,timeout=30))
for attempt in range(60):
 try: stats=req('/system_stats');break
 except Exception: time.sleep(2)
else: raise RuntimeError('Comfy unavailable')
(A/'ai_system_stats.json').write_text(json.dumps(stats,indent=2))
materials=['dense flowing sea grass meadow, clusters of curved broad kelp blades, teal jade turquoise, sparse luminous leaf tips, organic soft coral moss','ancient submerged limestone paving, four large irregular rounded rectangular stone slabs, staggered narrow dark joints, weathered pale blue grey limestone, chipped bevels, sparse barnacles, tiny brass inlay','deep sea sediment sand, quiet dark blue slate silt, soft current ripple sand patterns, fine layered grains, very sparse small seashells']
for k,mat in enumerate(materials):
 prompt='seamless top-down orthographic game ground material texture filling the whole image, '+mat+', hand-painted, Hades / Bastion style ground texture, soft brush strokes, painterly elegant material, clear medium frequency detail, restrained contrast, flat diffuse lighting, no perspective, no scene, no border'
 graph={'1':{'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':'sd_xl_base_1.0.safetensors'}},'2':{'class_type':'CLIPTextEncode','inputs':{'text':prompt,'clip':['1',1]}},'3':{'class_type':'CLIPTextEncode','inputs':{'text':'photograph, photorealistic, perspective, isometric, text, watermark, grid, frame, noisy, speckled, harsh shadows, glossy, neon','clip':['1',1]}},'4':{'class_type':'EmptyLatentImage','inputs':{'width':1024,'height':1024,'batch_size':1}},'5':{'class_type':'KSampler','inputs':{'seed':982073+k,'steps':28,'cfg':6.0,'sampler_name':'dpmpp_2m','scheduler':'karras','denoise':1.0,'model':['1',0],'positive':['2',0],'negative':['3',0],'latent_image':['4',0]}},'6':{'class_type':'VAEDecode','inputs':{'samples':['5',0],'vae':['1',2]}},'7':{'class_type':'SaveImage','inputs':{'images':['6',0],'filename_prefix':f'material_{k}'}}}
 (A/f'ai_workflow_{k}.json').write_text(json.dumps(graph,indent=2))
 job=req('/prompt',{'prompt':graph});pid=job['prompt_id'];print('submitted',k,pid,flush=True)
 for _ in range(600):
  hist=req('/history/'+pid)
  if pid in hist:
   (A/f'ai_history_{k}.json').write_text(json.dumps(hist,indent=2))
   assert hist[pid]['status']['status_str']=='success',hist
   print('done',k,flush=True);break
  time.sleep(2)
 else: raise TimeoutError(pid)
