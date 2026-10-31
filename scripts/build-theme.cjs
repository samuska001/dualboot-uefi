// Development-only format conversion. Firmware loads the checked-in BMP files directly.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.resolve(__dirname,'..');
async function bmp24(input, destination, matte) {
  const {data,info} = await sharp(input).flatten({background:matte}).removeAlpha().raw().toBuffer({resolveWithObject:true});
  if(info.channels!==3)throw new Error('Expected RGB data');
  const stride=(info.width*3+3)&~3, size=54+stride*info.height;
  const result=Buffer.alloc(size);
  result.write('BM'); result.writeUInt32LE(size,2); result.writeUInt32LE(54,10); result.writeUInt32LE(40,14);
  result.writeInt32LE(info.width,18); result.writeInt32LE(info.height,22); result.writeUInt16LE(1,26); result.writeUInt16LE(24,28); result.writeUInt32LE(stride*info.height,34);
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
    const source=(y*info.width+x)*3, target=54+(info.height-y-1)*stride+x*3;
    result[target]=data[source+2];result[target+1]=data[source+1];result[target+2]=data[source];
  }
  fs.writeFileSync(destination,result);
}
async function bmp32(input, destination) {
  const {data,info} = await sharp(input).resize(512,512,{fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(info.channels!==4)throw new Error('Expected RGBA data');
  const result=Buffer.alloc(54+info.width*info.height*4);
  result.write('BM');result.writeUInt32LE(result.length,2);result.writeUInt32LE(54,10);result.writeUInt32LE(40,14);
  result.writeInt32LE(info.width,18);result.writeInt32LE(info.height,22);result.writeUInt16LE(1,26);result.writeUInt16LE(32,28);result.writeUInt32LE(result.length-54,34);
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
    const source=(y*info.width+x)*4,target=54+((info.height-y-1)*info.width+x)*4;
    result[target]=data[source+2];result[target+1]=data[source+1];result[target+2]=data[source];result[target+3]=data[source+3];
  }
  fs.writeFileSync(destination,result);
}
(async()=>{
 const theme=path.join(root,'themes/night');fs.mkdirSync(theme,{recursive:true});
 await bmp24(path.join(root,'design/profiles/assets/background.png'),path.join(theme,'background.bmp'),'#080b12');
 await bmp32(path.join(root,'design/profiles/assets/windows.png'),path.join(theme,'windows.bmp'));
 await bmp32(path.join(root,'design/profiles/assets/steamos.png'),path.join(theme,'steamos.bmp'));
 console.log('Public theme: opaque RGB24 background and transparent BGRA32 icons; original logo shapes preserved.');
})().catch(error=>{console.error(error);process.exit(1)});
