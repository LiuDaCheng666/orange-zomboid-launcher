const path=require('node:path'),os=require('node:os');
try {module.exports=require('playwright');}
catch {module.exports=require(process.env.ORANGE_PLAYWRIGHT_MODULE||path.join(os.homedir(),'.cache','codex-runtimes','codex-primary-runtime','dependencies','node','node_modules','playwright'));}
