const path = require("path");
const express = require("express");
const cors = require("cors");
const jsonServer = require("json-server");

const HOST = "127.0.0.1";
const PORT = 60375;

const app = express();

// ✅ CORS (so you can also hit API from other origins if needed)
app.use(cors());

// ✅ Serve your HTML/JS from the same origin (best fix)
app.use(express.static(path.join(__dirname, "public")));

// json-server router for db.json
const router = jsonServer.router(path.join(__dirname, "db.json"));
const middlewares = jsonServer.defaults();

// json-server middlewares
app.use(middlewares);
app.use(jsonServer.bodyParser);

// Mount API under /api
app.use("/api", router);

// Helpful default route
app.get("/", (req, res) => {
  res.redirect("/temp.html");
});

app.listen(PORT, HOST, () => {
  console.log(`UI:  http://${HOST}:${PORT}/temp.html`);
  console.log(`API: http://${HOST}:${PORT}/api/supervisory-info-by-casemgr`);
});
