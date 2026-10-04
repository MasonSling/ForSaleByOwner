import { mkdirSync, copyFileSync } from 'node:fs';
const dest = 'src/ForSaleByOwner.Web/wwwroot/vendor';
mkdirSync(dest, {recursive:true});
copyFileSync('node_modules/htmx.org/dist/htmx.min.js', `${dest}/htmx.min.js`);
copyFileSync('node_modules/@alpinejs/csp/dist/cdn.min.js', `${dest}/alpine.min.js`);
