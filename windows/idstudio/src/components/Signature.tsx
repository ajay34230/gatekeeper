import React from 'react';

/** Bearer signature block: the real signature image captured in the Studio, or an empty signing line. */
export const BearerSignature: React.FC<{ imageUrl: string; name: string; signedAt: string; check: string }> = ({ imageUrl, name, signedAt, check }) => (
  <div className="flex flex-col items-center justify-center w-full">
    <div className="w-full border-b border-white/20 pb-0.5 relative flex items-center justify-center min-h-[38px] h-10">
      {imageUrl
        ? <img src={imageUrl} alt="Signature of bearer" className="h-10 max-w-[130px] object-contain drop-shadow-[0_1px_2px_rgba(0,0,0,0.5)]" />
        : <span className="text-[6px] font-mono text-slate-500 self-end pb-0.5">&nbsp;</span>}
    </div>
    <div className="w-full text-center mt-1 space-y-0.5">
      <div className="text-[7.5px] font-bold tracking-wider text-slate-200 uppercase">SIGNATURE OF BEARER</div>
      <div className="flex items-center justify-center gap-1.5 text-[6.5px] font-mono text-slate-400">
        {imageUrl ? <span className="text-emerald-400 font-semibold">SIGNED</span> : <span className="text-slate-500">SIGN ON ISSUE</span>}
        {signedAt && <><span>•</span><span>{signedAt}</span></>}
      </div>
      <div className="text-[5.5px] font-mono tracking-tight text-slate-500 truncate max-w-[130px] mx-auto" title="SHA-256 fingerprint of the printed details">
        CHECK: {check || '—'}
      </div>
    </div>
    <span className="sr-only">{name}</span>
  </div>
);
