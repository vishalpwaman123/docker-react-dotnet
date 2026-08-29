import { buildFailureResponse } from "../models/auth";

/**
 * Turns whatever axios threw into a normal AuthResponse, so the pages can
 * handle failures the same way whether they came from the API or not.
 *
 * There are two cases:
 *   1. The API answered, but with a non-2xx status (400, 401, 409...).
 *      The body is still our AuthResponse envelope, so we use it as-is.
 *   2. Nothing answered - server down, wrong URL, timeout, blocked
 *      certificate. There is no error.response, so we make a friendly message.
 *
 * @param {unknown} error
 * @returns {import("../models/auth").AuthResponse}
 */
export function toAuthResponse(error) {
  // Case 1: the API replied with an error status and our envelope.
  if (error.response && error.response.data) {
    return error.response.data;
  }

  // Case 2: the request never got an answer.
  return buildFailureResponse(
    "Unable to reach the server. Please try again."
  );
}
