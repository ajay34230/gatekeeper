import React from 'react';
import { CardDesign, Soldier } from '../types';
import { cardSize } from '../themes';
import { CardFront } from './CardFront';
import { CardBack } from './CardBack';
import { ModernCard } from './ModernCard';
import { PortraitFront, PortraitBack } from './CardPortrait';

/** One side of a card in the chosen size and orientation (portrait = neck / lanyard layout). */
export const CardFace: React.FC<{ side: 'front' | 'back'; soldier: Soldier; design: CardDesign; check?: string; className?: string }> = ({ side, soldier, design, check = '', className }) => {
  if (design.layout === 'modernSplit' || design.layout === 'modernSingle') return <ModernCard side={side} soldier={soldier} design={design} className={className} />;
  const portrait = cardSize(design).portrait;
  if (side === 'front') return portrait ? <PortraitFront soldier={soldier} design={design} check={check} className={className} /> : <CardFront soldier={soldier} design={design} check={check} className={className} />;
  return portrait ? <PortraitBack soldier={soldier} design={design} className={className} /> : <CardBack soldier={soldier} design={design} className={className} />;
};
