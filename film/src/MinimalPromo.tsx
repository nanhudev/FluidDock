import React from 'react';
import {AbsoluteFill,Audio,Img,OffthreadVideo,Sequence,interpolate,staticFile,spring,useCurrentFrame,useVideoConfig} from 'remotion';
import {theme,clamp} from './theme';
import {Bot,Entrance} from './components';

// Native coordinates remain in a 1600 x 900 logical stage. Camera paths
// use eased absolute keyframes so focus follows a gesture without a cut.
const cameras=[
 {t:0,x:800,y:795,z:1.9}, {t:3,x:800,y:795,z:1.96},
 {t:4,x:800,y:450,z:1.08}, {t:10,x:800,y:450,z:1.12},
 {t:11.5,x:800,y:450,z:1.08},
 {t:13,x:800,y:340,z:1.5}, {t:14.5,x:800,y:340,z:1.5},
 {t:16,x:800,y:610,z:1.2}, {t:20.5,x:800,y:660,z:1.25},
 {t:22.5,x:800,y:340,z:1.5}, {t:24,x:800,y:340,z:1.5},
 {t:25.5,x:740,y:780,z:2.05}, {t:26.5,x:740,y:810,z:2.3},
 {t:29,x:740,y:810,z:2.3},
 {t:30.5,x:800,y:340,z:1.42}, {t:32,x:800,y:340,z:1.42},
 {t:33.5,x:850,y:610,z:1.32}, {t:38,x:870,y:640,z:1.32},
 {t:39.5,x:930,y:812,z:2.35}, {t:41.5,x:930,y:812,z:2.35},
 {t:43,x:800,y:800,z:1.4}, {t:46,x:800,y:800,z:1.4},
 {t:47.5,x:800,y:450,z:1.12}, {t:55.5,x:800,y:450,z:1.16},
 {t:57,x:800,y:450,z:1.16}, {t:58,x:620,y:610,z:1.3},
 {t:59.5,x:440,y:805,z:2.05}, {t:62,x:440,y:805,z:2.05},
 {t:64,x:800,y:800,z:1.4}, {t:66,x:800,y:800,z:1.43},
];
const Native:React.FC=()=>{
 const f=useCurrentFrame(),{fps,width,height}=useVideoConfig();const time=f/fps;
 let right=cameras.findIndex(c=>c.t>time);if(right<0)right=cameras.length-1;
 const a=cameras[Math.max(0,right-1)],b=cameras[right];
 const mix=(k:'x'|'y'|'z')=>interpolate(time,[a.t,b.t===a.t?a.t+.01:b.t],[a[k],b[k]],{...clamp,easing:theme.ease.inOut});
 const z=mix('z'),x=width/2-mix('x')*width/1600*z,y=height/2-mix('y')*height/900*z;
 const enter=spring({frame:f,fps,config:theme.spring.smooth});
 const exit=interpolate(time,[65.8,66],[1,0],{...clamp,easing:theme.ease.in});
 return <AbsoluteFill style={{overflow:'hidden',opacity:Math.min(1,enter)*exit}}>
  <AbsoluteFill style={{transformOrigin:'0 0',transform:`translate(${x}px,${y+(1-enter)*18}px) scale(${z})`}}>
   {time<4?<Img src={staticFile('empty-dock.png')} style={{width:'100%',height:'100%'}}/>:<OffthreadVideo src={staticFile('native-demo.mp4')} muted style={{width:'100%',height:'100%',objectFit:'cover'}}/>}
   <Img src={staticFile('background-edge.png')} style={{position:'absolute',left:0,top:'calc(100% - 1px)',width:'100%',height:'150%'}}/>
  </AbsoluteFill>
  <div style={{position:'absolute',left:64,top:44,color:theme.colors.ink,fontWeight:700,fontSize:25,letterSpacing:-1}}>dockbot</div>
 </AbsoluteFill>;
};
const Identity:React.FC<{end?:boolean}>=({end=false})=>{
 const f=useCurrentFrame(),{fps}=useVideoConfig();
 const exit=interpolate(f,[1.78*fps,2*fps],[1,0],{...clamp,easing:theme.ease.in});
 return <AbsoluteFill style={{alignItems:'center',justifyContent:'center',opacity:exit}}>
  <Entrance><Bot size={132}/></Entrance>
  <Entrance delay={.12}><div style={{fontSize:110,fontWeight:700,letterSpacing:-6,lineHeight:1.3}}>dockbot</div></Entrance>
  <Entrance delay={.25}><div style={{fontSize:24,color:theme.colors.muted,marginTop:22}}>{end?'github.com/nanhudev/dockbot':'Windows 妗岄潰鍔╂墜'}</div></Entrance>
 </AbsoluteFill>;
};
export const Promo:React.FC=()=>{const {fps}=useVideoConfig();return <AbsoluteFill style={{fontFamily:theme.fonts.display,color:theme.colors.ink,background:theme.colors.bg}}>
 <style>{`@font-face{font-family:DockSans;src:url('${staticFile('fonts/ui.ttc')}');font-weight:400;}@font-face{font-family:DockSans;src:url('${staticFile('fonts/ui-bold.ttc')}');font-weight:700;}`}</style>
 <AbsoluteFill style={{background:`radial-gradient(ellipse at 50% 38%,${theme.colors.surface},${theme.colors.bg} 72%)`}}/>
 <Sequence durationInFrames={2*fps}><Identity/></Sequence>
 <Sequence from={2*fps} durationInFrames={66*fps}><Native/></Sequence>
 <Sequence from={68*fps} durationInFrames={2*fps}><Identity end/></Sequence>
 <Audio src={staticFile('score-master.wav')}/>
 </AbsoluteFill>};

