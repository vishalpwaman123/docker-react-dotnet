import axios from "axios";

// The one shared axios instance for the whole app.
// Every API call goes through this, so the base URL and headers
// are configured in a single place.
const axiosClient = axios.create({
  // Read from .env - never hardcode the URL in a component.
  baseURL: process.env.REACT_APP_API_BASE_URL,

  headers: {
    "Content-Type": "application/json",
  },

  // Give up after 10 seconds instead of leaving the button spinning forever.
  timeout: 10000,
});

export default axiosClient;
