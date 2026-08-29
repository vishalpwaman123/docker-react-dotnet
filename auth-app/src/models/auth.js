// Shapes of everything we send to, and receive from, the auth API.
// Components build requests with these helpers instead of writing
// object literals inline, so the field names live in one place.

/**
 * What POST /api/auth/signup expects in its body.
 *
 * @typedef {Object} SignUpRequest
 * @property {string} email
 * @property {string} password
 * @property {string} confirmPassword
 */

/**
 * What POST /api/auth/signin expects in its body.
 *
 * @typedef {Object} SignInRequest
 * @property {string} email
 * @property {string} password
 */

/**
 * The user details the API returns. It is null when the call fails.
 *
 * @typedef {Object} UserResponse
 * @property {number} id
 * @property {string} email
 */

/**
 * The single envelope BOTH endpoints return, for success and failure alike.
 *
 * @typedef {Object} AuthResponse
 * @property {boolean} success
 * @property {string} message
 * @property {UserResponse|null} user
 * @property {string[]} errors
 */

/**
 * Builds the body for a sign up call.
 *
 * @param {string} email
 * @param {string} password
 * @param {string} confirmPassword
 * @returns {SignUpRequest}
 */
export function buildSignUpRequest(email, password, confirmPassword) {
  return {
    email: email,
    password: password,
    confirmPassword: confirmPassword,
  };
}

/**
 * Builds the body for a sign in call.
 *
 * @param {string} email
 * @param {string} password
 * @returns {SignInRequest}
 */
export function buildSignInRequest(email, password) {
  return {
    email: email,
    password: password,
  };
}

/**
 * Builds an AuthResponse ourselves, for problems that never reached the API
 * (for example the network being down). This lets the pages handle every
 * outcome with the same shape, so they only ever read .success/.message/.errors.
 *
 * @param {string} message
 * @param {string[]} [errors]
 * @returns {AuthResponse}
 */
export function buildFailureResponse(message, errors = []) {
  return {
    success: false,
    message: message,
    user: null,
    errors: errors,
  };
}
