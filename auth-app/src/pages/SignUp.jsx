import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { signUp } from "../api/authApi";
import { toAuthResponse } from "../api/apiError";
import { buildSignUpRequest } from "../models/auth";

function SignUp() {
  // One piece of state per input field (controlled inputs)
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  // Holds the validation message shown above the form
  const [errorMessage, setErrorMessage] = useState("");

  // The API can return several problems at once, so we keep a list too
  const [errorList, setErrorList] = useState([]);

  // Message shown when the account was created
  const [successMessage, setSuccessMessage] = useState("");

  // True while the API call is in flight, used to disable the button
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Used to move to the Sign In page without reloading the browser
  const navigate = useNavigate();

  // Runs when the form is submitted
  async function handleSubmit(event) {
    event.preventDefault();

    // Start clean: drop any messages left over from the last attempt
    setErrorMessage("");
    setErrorList([]);
    setSuccessMessage("");

    // Rule 1: every field must be filled in
    if (email === "" || password === "" || confirmPassword === "") {
      setErrorMessage("Please fill in all fields.");
      return;
    }

    // Rule 2: the two passwords must be the same
    if (password !== confirmPassword) {
      setErrorMessage("Passwords do not match.");
      return;
    }

    // Client checks passed, so now we can call the API
    setIsSubmitting(true);

    try {
      const request = buildSignUpRequest(email, password, confirmPassword);
      const authResponse = await signUp(request);

      if (authResponse.success) {
        // Show the API's message, then move to the Sign In page
        setSuccessMessage(authResponse.message);
        setTimeout(() => navigate("/signin"), 1500);
      } else {
        // A 2xx response that still reports failure
        setErrorMessage(authResponse.message);
        setErrorList(authResponse.errors);
      }
    } catch (error) {
      // Non-2xx status (400 / 409) or no answer at all
      const authResponse = toAuthResponse(error);
      setErrorMessage(authResponse.message);
      setErrorList(authResponse.errors);
    } finally {
      // Always re-enable the button, whatever happened
      setIsSubmitting(false);
    }
  }

  return (
    // Full-height white page, form centered horizontally and vertically
    <div className="d-flex justify-content-center align-items-center min-vh-100 bg-white">
      <div className="card shadow-sm p-4" style={{ width: "400px" }}>
        {/* Page heading above the form */}
        <h2 className="text-center mb-4">Sign Up</h2>

        {/* Validation or API error, only shown when there is one */}
        {errorMessage && (
          <div className="alert alert-danger" role="alert">
            {errorMessage}

            {/* Every problem the API sent back */}
            {errorList.length > 0 && (
              <ul className="mb-0 mt-2">
                {errorList.map((error, index) => (
                  <li key={index}>{error}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        {/* Shown after the account is created */}
        {successMessage && (
          <div className="alert alert-success" role="alert">
            {successMessage}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          {/* Email field */}
          <div className="mb-3">
            <label className="form-label">Email</label>
            <input
              type="email"
              className="form-control"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>

          {/* Password field */}
          <div className="mb-3">
            <label className="form-label">Password</label>
            <input
              type="password"
              className="form-control"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>

          {/* Confirm password field */}
          <div className="mb-3">
            <label className="form-label">Confirm Password</label>
            <input
              type="password"
              className="form-control"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
            />
          </div>

          {/* Main action button, disabled while the request is running */}
          <button
            type="submit"
            className="btn btn-primary w-100 mb-2"
            disabled={isSubmitting}
          >
            {isSubmitting ? "Signing Up..." : "Sign Up"}
          </button>

          {/* Switch to the Sign In page */}
          <button
            type="button"
            className="btn btn-outline-secondary w-100"
            onClick={() => navigate("/signin")}
          >
            Sign In
          </button>
        </form>
      </div>
    </div>
  );
}

export default SignUp;
