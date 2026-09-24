import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
  plugins: [react(), tailwindcss()],

  base: "/ISO/",

  build: {
    target: "chrome109",
    cssTarget: "chrome109",
    cssMinify: "lightningcss",
  },

  server: {
    host: "0.0.0.0",
    port: 5173,

    hmr: {
      host: "10.8.252.215",
      port: 5173,
    },

    proxy: {
      "/api": {
        target: "http://localhost:5170",
        changeOrigin: true,
      },
    },
  },

  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
});
