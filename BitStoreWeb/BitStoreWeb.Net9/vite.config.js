const path = require("node:path");
const { defineConfig } = require("vite");

module.exports = defineConfig({
  build: {
    outDir: path.resolve(__dirname, "wwwroot/dist"),
    emptyOutDir: true,
    rollupOptions: {
      input: path.resolve(__dirname, "frontend/main.js"),
      output: {
        entryFileNames: "app.js",
        chunkFileNames: "chunks/[name]-[hash].js",
        assetFileNames: (assetInfo) => {
          if (assetInfo.name && assetInfo.name.endsWith(".css")) {
            return "app.css";
          }

          return "assets/[name]-[hash][extname]";
        },
      },
    },
  },
});
