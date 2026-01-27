const path = require("path");
const express = require("express");
const cors = require("cors");
const jsonServer = require("json-server");

const HOST = "127.0.0.1";
const PORT = 60375;

const app = express();

// Enable CORS (safe for local dev)
app.use(cors());

// Serve static files from /public (temp.html, app.js)
app.use(express.static(path.join(__dirname, "public")));

// json-server setup for db.json
const router = jsonServer.router(path.join(__dirname, "db.json"));
const middlewares = jsonServer.defaults();

app.use(middlewares);
app.use(jsonServer.bodyParser);

// Mount db.json routes under /api
app.use("/api", router);

// Default route
app.get("/", (req, res) => res.redirect("/temp.html"));

app.listen(PORT, HOST, () => {
  console.log(`UI:  http://${HOST}:${PORT}/temp.html`);
  console.log(`API: http://${HOST}:${PORT}/api/supervisory-info-by-casemgr`);
});
