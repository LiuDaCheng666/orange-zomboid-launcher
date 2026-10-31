const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const file=process.env.OZL_SERVER_NAME_APP_JS||path.resolve(__dirname,'../Orange3DTool/Web/app.js');
const source=fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'').split(/\r?\n/);
const actual=(name,context={})=>{const text=source.find(s=>s.startsWith('function '+name+'('));assert(text,'Missing actual production function '+name);return vm.runInNewContext('('+text+')',context);};
const tests=[],test=(name,run)=>tests.push({name,run});
const merge=actual('mergeServerMetric');
test('successful query with unusable name clears a prior live name so configured title can be used',()=>assert.equal(merge({name:'Previous live title'},{status:'online',name:null,players:4}).name,null));
test('successful usable live title replaces the old one',()=>assert.equal(merge({name:'Previous live title'},{status:'online',name:'Current title'}).name,'Current title'));
for(const status of ['unknown','partial','querying'])test(status+' preserves prior known title during incomplete query',()=>assert.equal(merge({name:'Known title'},{status,name:null}).name,'Known title'));
function hall(query){const row={id:'hall-one',name:'Existing hall title',host:'127.0.0.1',port:16261,players:1,maxPlayers:80},context={state:{serverHall:{servers:[row],message:''},loading:{},errors:{},hallProgress:''},hallRows:()=>[row],serverQueryResults:()=>({'hall-one':query}),esc:value=>String(value??''),serverStatusMarkup:()=>'',serverQueryPending:()=>false,metricBadges:()=>'',hallPing:()=>0,richDescription:()=>''};return actual('hallResults',context)();}
test('hall retains its source title when online query name is unusable',()=>assert.match(hall({name:null,status:'online',players:4,maxPlayers:80}),/<h3>Existing hall title<\/h3>/));
test('hall retains its source title while query is unavailable',()=>assert.match(hall({name:null,status:'unknown'}),/<h3>Existing hall title<\/h3>/));
test('hall displays a usable current live title',()=>assert.match(hall({name:'Valid live title',status:'online'}),/<h3>Valid live title<\/h3>/));
let failed=0;for(const item of tests){try{item.run();console.log('PASS '+item.name);}catch(e){failed++;console.error('FAIL '+item.name+'\n'+e.message);}}
console.log((tests.length-failed)+'/'+tests.length+' production metric-name cases passed');if(failed)process.exitCode=1;
