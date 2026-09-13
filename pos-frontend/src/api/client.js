import axios from "axios";

// Matches Properties/launchSettings.json's HTTPS profile. The dev cert is self-signed,
// so the first request will fail in the browser until you open this URL directly once
// and accept the certificate warning (or run `dotnet dev-certs https --trust`).
const API_BASE_URL = "https://localhost:61720/api";

const client = axios.create({
  baseURL: API_BASE_URL,
});

client.interceptors.request.use((config) => {
  const token = localStorage.getItem("pos_token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("pos_token");
      localStorage.removeItem("pos_user");
      if (window.location.pathname !== "/login") {
        window.location.href = "/login";
      }
    }
    return Promise.reject(error);
  }
);

export default client;
