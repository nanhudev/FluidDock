import React from 'react';
import {registerRoot,Composition} from 'remotion';
import {Promo} from './MinimalPromo';
import {Portrait,films} from '../shorts/Portrait';
const Root:React.FC=()=> <><Composition id="DockBotPromo" component={Promo} durationInFrames={70*30} fps={30} width={1920} height={1080}/>{films.map(f=><Composition key={f.id} id={f.id.replaceAll('_','-')+'-Graphics'} component={Portrait} defaultProps={{filmId:f.id}} durationInFrames={Math.round(f.duration*30)} fps={30} width={1080} height={1920}/>)}</>;
registerRoot(Root);
