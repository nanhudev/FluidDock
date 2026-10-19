import React from 'react';
import {AbsoluteFill,Sequence,staticFile,useCurrentFrame,useVideoConfig,interpolate,spring} from 'remotion';
import {theme,clamp} from '../src/theme';
import {Bot,Entrance} from '../src/components';
import manifest from './manifest.json';
export const films=manifest.films;
type Scene=typeof films[number]['scenes'][number];
const code:Record<string,string[]>={
 'code-intent':['IntentModel.cs','public bool Available {get{return model!=null;}}','var scores=(double[])model.prior.Clone();'],
 'code-motion':['FluidMotion.cs','CompositionTarget.Rendering+=Frame;','CompositionTarget.Rendering-=Frame;'],
 'code-assistant':['AssistantPanel.cs','if(pending==null)return;','if(!pending.Actionable)return;','dock.Execute(plan)'],
};
const Shot:React.FC<{scene:Scene}>=({scene})=>{
 const f=useCurrentFrame(),{fps}=useVideoConfig();
 const p=spring({frame:f,fps,config:theme.spring.smooth});
 const exit=interpolate(f,[scene.duration*fps-.16*fps,scene.duration*fps],[1,0],{...clamp,easing:theme.ease.in});
 const lines=scene.title.split('\n');const weight=Math.max(...lines.map(s=>[...s].reduce((n,c)=>n+(/[\u3400-\u9fff]/.test(c)?1:.55),0)));
 const font=scene.title==='17KB。'?200:Math.min(lines.length>1?84:94,910/weight);
 const end=scene.kind==='end';const hero=scene.kind==='hero';const sourceCode=code[scene.kind];
 const accent=scene.title.includes('17KB')||scene.kind==='code-intent';
 return <AbsoluteFill style={{opacity:Math.min(1,p)*exit}}>
  {!end&&<div style={{position:'absolute',left:76,top:140,fontSize:28,fontWeight:700,color:theme.colors.muted,letterSpacing:-.8}}>dockbot <span style={{fontWeight:400,fontSize:21,marginLeft:20}}>Windows</span></div>}
  {!end&&<Entrance delay={.03}><div style={{position:'absolute',left:76,top:260,width:920,fontSize:font,fontWeight:700,lineHeight:1.18,letterSpacing:-2.5,whiteSpace:'pre-line',color:accent?theme.colors.hero:theme.colors.ink}}>{scene.title}</div></Entrance>}
  {!end&&scene.sub&&<Entrance delay={.14}><div style={{position:'absolute',left:80,top:260+font*1.18*lines.length+30,width:910,fontSize:32,lineHeight:1.5,color:theme.colors.muted}}>{scene.sub}</div></Entrance>}
  {(hero||end)&&<div style={{position:'absolute',left:end?410:390,top:end?650:850,transform:`translateY(${(1-p)*22}px) scale(${.96+.04*p})`}}><Bot size={end?260:300}/></div>}
  {hero&&<div style={{position:'absolute',left:80,right:80,top:1300,textAlign:'center',fontSize:27,color:theme.colors.muted,letterSpacing:3}}>SMALL MODEL · USEFUL ACTIONS</div>}
  {sourceCode&&<Entrance delay={.22}><div style={{position:'absolute',left:76,top:760,width:928,padding:38,boxSizing:'border-box',borderRadius:30,background:theme.colors.surface,boxShadow:'0 24px 80px rgba(0,0,0,.2)'}}>
   <div style={{fontSize:29,color:theme.colors.hero,marginBottom:42}}>{sourceCode[0]}</div>
   {sourceCode.slice(1).map((line,i)=><Entrance key={line} delay={.1*i}><div style={{fontSize:29,fontFamily:'Consolas,monospace',lineHeight:2.3,color:theme.colors.ink,overflowWrap:'anywhere'}}>{line}</div></Entrance>)}
  </div></Entrance>}
  {scene.tags.length>0&&<div style={{position:'absolute',left:80,top:1420,display:'flex',gap:16}}>{scene.tags.map((tag,i)=><Entrance key={tag} delay={.18+i*.1}><div style={{padding:'16px 22px',borderRadius:30,background:theme.colors.surface,fontSize:27,color:theme.colors.hero}}>{tag}</div></Entrance>)}</div>}
  {end&&<>
   <Entrance delay={.16}><div style={{position:'absolute',top:1060,width:'100%',textAlign:'center',fontSize:105,fontWeight:700,letterSpacing:-5}}>DockBot</div></Entrance>
   <Entrance delay={.26}><div style={{position:'absolute',top:1225,width:'100%',textAlign:'center',fontSize:32,color:theme.colors.hero}}>Open Source · Windows</div></Entrance>
   <Entrance delay={.34}><div style={{position:'absolute',top:1320,width:'100%',textAlign:'center',fontSize:36,color:theme.colors.muted}}>github.com/nanhudev/dockbot</div></Entrance>
  </>}
  {!end&&<div style={{position:'absolute',left:80,bottom:115,fontSize:21,color:theme.colors.muted}}>v0.1.1 · 独立演示配置</div>}
 </AbsoluteFill>;
};
export const Portrait:React.FC<{filmId:string}>=({filmId})=>{
 const {fps}=useVideoConfig();const f=useCurrentFrame();const film=films.find(v=>v.id===filmId)!;
 return <AbsoluteFill style={{background:theme.colors.bg,color:theme.colors.ink,fontFamily:theme.fonts.display}}>
  <style>{`@font-face{font-family:DockSans;src:url('${staticFile('fonts/ui.ttc')}');font-weight:400;}@font-face{font-family:DockSans;src:url('${staticFile('fonts/ui-bold.ttc')}');font-weight:700;}`}</style>
  <AbsoluteFill style={{background:`radial-gradient(ellipse at ${50+Math.sin(f/fps*.3)*3}% 46%,${theme.colors.surface},${theme.colors.bg} 74%)`}}/>
  {film.scenes.map((s,i)=><Sequence key={i} from={Math.round(s.at*fps)} durationInFrames={Math.round(s.duration*fps)}><Shot scene={s}/></Sequence>)}
 </AbsoluteFill>;
};
