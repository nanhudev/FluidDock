import React from 'react';
import {AbsoluteFill,Img,interpolate,spring,staticFile,useCurrentFrame,useVideoConfig} from 'remotion';
import {theme,clamp} from './theme';
export const Entrance:React.FC<{delay?:number;children:React.ReactNode;style?:React.CSSProperties}>=({delay=0,children,style})=>{
 const frame=useCurrentFrame(),{fps}=useVideoConfig();const p=spring({frame:frame-delay*fps,fps,config:theme.spring.smooth});
 return <div style={{opacity:Math.min(1,p),transform:`translateY(${(1-p)*34}px) scale(${.96+.04*p})`,...style}}>{children}</div>;
};
export const Bot:React.FC<{size?:number}>=({size=170})=>{const f=useCurrentFrame(),{fps}=useVideoConfig();return <Img src={staticFile('buddy.svg')} style={{width:size,height:size,transform:`translateY(${Math.sin(f/fps*1.2)*3}px) rotate(${Math.sin(f/fps*.6)*1.2}deg)`}}/>};
