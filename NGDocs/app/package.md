## app\package.json

```json
{
  "name": "app",
  "version": "0.0.0",
  "scripts": {
    "ng": "ng",
    "start": "ng serve --no-ssr --port 4200 --proxy-config proxy.conf.json",
    "build": "ng build",
    "watch": "ng build --watch --configuration development",
    "test": "ng test",
    "serve:ssr:app": "node dist/app/server/server.mjs"
  },
  "prettier": {
    "printWidth": 100,
    "singleQuote": true,
    "overrides": [
      {
        "files": "*.html",
        "options": {
          "parser": "angular"
        }
      }
    ]
  },
  "private": true,
  "packageManager": "npm@10.9.0",
  "dependencies": {
    "@angular/common": "^21.1.0",
    "@angular/compiler": "^21.1.0",
    "@angular/core": "^21.1.0",
    "@angular/forms": "^21.1.0",
    "@angular/platform-browser": "^21.1.0",
    "@angular/platform-server": "^21.1.0",
    "@angular/router": "^21.1.0",
    "@angular/ssr": "^21.1.1",
    "bootstrap": "^5.3.8",
    "bootstrap-icons": "^1.13.1",
    "express": "^5.1.0",
    "rxjs": "~7.8.0",
    "tslib": "^2.3.0"
  },
  "devDependencies": {
    "@angular/build": "^21.1.1",
    "@angular/cli": "^21.1.1",
    "@angular/compiler-cli": "^21.1.0",
    "@types/express": "^5.0.1",
    "@types/node": "^20.17.19",
    "jsdom": "^27.1.0",
    "typescript": "~5.9.2",
    "vitest": "^4.0.8"
  }
}
```

 *  Executing task: npm run dev 


> ngdocs@0.1.0 dev
> concurrently -n APP,API,DATA -c auto "npm:dev:app" "npm:dev:api" "npm:dev:data"

[APP] 
[APP] > ngdocs@0.1.0 dev:app
[APP] > npm --prefix app run start
[APP] 
[DATA] 
[DATA] > ngdocs@0.1.0 dev:data
[DATA] > npm --prefix data run dev
[DATA] 
[API] 
[API] > ngdocs@0.1.0 dev:api
[API] > npm --prefix server run dev
[API] 
[DATA] 
[DATA] > ngdocs-data@0.1.0 dev
[DATA] > json-server --watch db.json --port 3002
[DATA] 
[API] 
[API] > ngdocs-server@0.1.0 dev
[API] > nodemon index.js
[API]
[APP]
[APP] > app@0.0.0 start
[APP] > ng serve --no-ssr --port 4200 --proxy-config proxy.conf.json
[APP]
[API] [nodemon] 3.1.11
[API] [nodemon] to restart at any time, enter `rs`
[API] [nodemon] watching path(s): *.*
[API] [nodemon] watching extensions: js,mjs,cjs,json
[API] [nodemon] starting `node index.js`
[DATA] 
[DATA]   \{^_^}/ hi!
[DATA]
[DATA]   Loading db.json
[DATA]   Done
[DATA] 
[DATA]   Resources
[DATA]   http://localhost:3002/workItems
[DATA]
[DATA]   Home
[DATA]   http://localhost:3002
[DATA]
[DATA]   Type s + enter at any time to create a snapshot of the database
[DATA]   Watching...
[DATA]
[API] [NGDocs API] http://localhost:3001
[APP] Error: Unknown argument: ssr
[APP] npm run dev:app exited with code 1