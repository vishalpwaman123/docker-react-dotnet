import axiosClient from "./axiosClient";

// The only two API calls in the app. Pages call these functions;
// they never touch axios themselves.

/**
 * Creates a new account.
 *
 * @param {import("../models/auth").SignUpRequest} signUpRequest
 * @returns {Promise<import("../models/auth").AuthResponse>}
 */
export async function signUp(signUpRequest) {
  const response = await axiosClient.post("/api/auth/signup", signUpRequest);
  return response.data;
}

/**
 * Signs an existing user in.
 *
 * @param {import("../models/auth").SignInRequest} signInRequest
 * @returns {Promise<import("../models/auth").AuthResponse>}
 */
export async function signIn(signInRequest) {
  const response = await axiosClient.post("/api/auth/signin", signInRequest);
  return response.data;
}
