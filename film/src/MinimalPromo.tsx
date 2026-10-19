import React from 'react';
import {AbsoluteFill,Audio,OffthreadVideo,Sequence,interpolate,staticFile,spring,useCurrentFrame,useVideoConfig} from 'remotion';
import {theme,clamp} from './theme';
import {Bot,Entrance} from './components';

const shots=[
 {at:2,len:3,source:0,zoom:1.8,y:-300,origin:'50% 100%',name:''},
 {at:5,len:5,source:3,zoom:1.1,y:-50,origin:'50% 50%',name:'一键配置'},
 {at:10,len:5,source:8,zoom:1.05,y:0,origin:'50% 100%',name:'添加软件'},
 {at:15,len:4,source:13,zoom:1.05,y:0,origin:'50% 100%',name:'调整顺序'},
 {at:19,len:5,source:17,zoom:1.05,y:0,origin:'50% 100%',name:'开发分组'},
 {at:24,len:4,source:22,zoom:1.42,y:-340,origin:'50% 100%',name:''},
 {at:28,len:8,source:26,zoom:1.04,y:-45,origin:'50% 50%',name:'电脑整理'},
 {at:36,len:2,source:34,zoom:1.42,y:-340,origin:'50% 100%',name:''},
];
const Native:React.FC<{shot:typeof shots[number]}>=({shot})=>{
 const f=useCurrentFrame(),{fps}=useVideoConfig();
 const enter=spring({frame:f,fps,config:theme.spring.smooth});
 const drift=interpolate(f,[0,shot.len*fps],[0,.012],{...clamp,easing:theme.ease.inOut});
 const exit=interpolate(f,[shot.len*fps-.16*fps,shot.len*fps],[1,0],{...clamp,easing:theme.ease.in});
 return <AbsoluteFill style={{overflow:'hidden',opacity:Math.min(1,enter)*exit}}>
  <OffthreadVideo src={staticFile('native-demo.mp4')} trimBefore={shot.source*fps} muted style={{width:'100%',height:'100%',objectFit:'cover',transformOrigin:shot.origin,transform:`translateY(${shot.y+(1-enter)*22}px) scale(${shot.zoom+drift})`}}/>
  <div style={{position:'absolute',left:64,top:44,color:theme.colors.ink,fontWeight:700,fontSize:25,letterSpacing:-1}}>dockbot</div>
  <Sequence layout="none" from={Math.round(.25*fps)} durationInFrames={Math.round(1.75*fps)}><Entrance><div style={{position:'absolute',right:64,top:50,fontSize:19,color:theme.colors.muted}}>{shot.name}</div></Entrance></Sequence>
 </AbsoluteFill>;
};
const Identity:React.FC<{end?:boolean}>=({end=false})=>{
 const f=useCurrentFrame(),{fps}=useVideoConfig();
 const exit=interpolate(f,[1.78*fps,2*fps],[1,0],{...clamp,easing:theme.ease.in});
 return <AbsoluteFill style={{alignItems:'center',justifyContent:'center',opacity:exit}}>
  <Entrance><Bot size={132}/></Entrance>
  <Entrance delay={.12}><div style={{fontSize:110,fontWeight:700,letterSpacing:-6,lineHeight:1.3}}>dockbot</div></Entrance>
  <Entrance delay={.25}><div style={{fontSize:24,color:theme.colors.muted,marginTop:22}}>{end?'github.com/nanhudev/dockbot':'Windows 桌面助手'}</div></Entrance>
 </AbsoluteFill>;
};
export const Promo:React.FC=()=>{const {fps}=useVideoConfig();return <AbsoluteFill style={{fontFamily:theme.fonts.display,color:theme.colors.ink,background:theme.colors.bg}}>
 <style>{`@font-face{font-family:DockSans;src:url('${staticFile('fonts/ui.ttc')}');font-weight:400;}@font-face{font-family:DockSans;src:url('${staticFile('fonts/ui-bold.ttc')}');font-weight:700;}`}</style>
 <AbsoluteFill style={{background:`radial-gradient(ellipse at 50% 38%,${theme.colors.surface},${theme.colors.bg} 72%)`}}/>
 <Sequence durationInFrames={2*fps}><Identity/></Sequence>
 {shots.map(s=><Sequence key={s.at} from={s.at*fps} durationInFrames={s.len*fps}><Native shot={s}/></Sequence>)}
 <Sequence from={38*fps} durationInFrames={2*fps}><Identity end/></Sequence>
 <Audio src={staticFile('score-master.wav')}/>
 </AbsoluteFill>};

