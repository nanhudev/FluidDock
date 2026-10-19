import React from 'react';
import {registerRoot,Composition} from 'remotion';
import {Promo} from './MinimalPromo';
const Root:React.FC=()=> <Composition id="DockBotPromo" component={Promo} durationInFrames={40*30} fps={30} width={1920} height={1080}/>;
registerRoot(Root);
