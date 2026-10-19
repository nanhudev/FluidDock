import {Easing} from 'remotion';
export const theme={
 colors:{bg:'#080E17',surface:'#111C29',hero:'#9AD9CE',ink:'#F1F5F6',muted:'#8B9DAE',line:'rgba(255,255,255,0.10)',shadow:'rgba(0,0,0,0.50)'},
 fonts:{display:'DockSans, sans-serif',body:'DockSans, sans-serif'},
 spring:{smooth:{damping:26,stiffness:135,mass:1},snappy:{damping:24,stiffness:230,mass:0.8}},
 ease:{out:Easing.bezier(.16,1,.3,1),in:Easing.bezier(.5,0,.9,.5),inOut:Easing.bezier(.65,0,.35,1)},
 bpm:120,
};
export const clamp={extrapolateLeft:'clamp' as const,extrapolateRight:'clamp' as const,easing:theme.ease.out};
