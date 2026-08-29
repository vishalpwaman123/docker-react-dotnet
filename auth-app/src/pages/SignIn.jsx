import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { signIn } from "../api/authApi";
import { toAuthResponse } from "../api/apiError";
import { buildSignInRequest } from "../models/auth";

function SignIn() {
  // One piece of state per input field (controlled inputs)
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  // Holds the validation message shown above the form
  const [errorMessage, setErrorMessage] = useState("");

  // The API can return several problems at once, so we keep a list too
  const [errorList, setErrorList] = useState([]);

  // Message shown after a successful sign in
  const [successMessage, setSuccessMessage] = useState("");

  // The email the API confirmed, shown under the success message
  const [signedInEmail, setSignedInEmail] = useState("");

  // True while the API call is in flight, used to disable the button
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Used to move to the Sign Up page without reloading the browser
  const navigate = useNavigate();

  // Runs when the form is submitted
  async function handleSubmit(event) {
    event.preventDefault();

    // Start clean: drop any messages left over from the last attempt
    setErrorMessage("");
    setErrorList([]);
    setSuccessMessage("");
    setSignedInEmail("");

    // Every field must be filled in
    if (email === "" || password === "") {
      setErrorMessage("Please fill in all fields.");
      return;
    }

    // Client checks passed, so now we can call the API
    setIsSubmitting(true);

    try {
      const request = buildSignInRequest(email, password);
      const authResponse = await signIn(request);

      if (authResponse.success) {
        // The API returns no token, so there is no session to start.
        // We show the message and stay on this page.
        setSuccessMessage(authResponse.message);

        if (authResponse.user) {
          setSignedInEmail(authResponse.user.email);
        }

        // Do not leave the password sitting in the box
        setPassword("");
      } else {
        // A 2xx response that still reports failure
        setErrorMessage(authResponse.message);
        setErrorList(authResponse.errors);
      }
    } catch (error) {
      // Non-2xx status (400 / 401) or no answer at all
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
        <h2 className="text-center mb-4">Sign In</h2>

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

        {/* Shown after a successful sign in */}
        {successMessage && (
          <div className="alert alert-success" role="alert">
            {successMessage}
            {signedInEmail && (
              <div className="mt-1">Signed in as: {signedInEmail}</div>
            )}
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

          {/* Main action button, disabled while the request is running */}
          <button
            type="submit"
            className="btn btn-primary w-100 mb-2"
            disabled={isSubmitting}
          >
            {isSubmitting ? "Signing In..." : "Sign In"}
          </button>

          {/* Switch to the Sign Up page */}
          <button
            type="button"
            className="btn btn-outline-secondary w-100"
            onClick={() => navigate("/signup")}
          >
            Sign Up
          </button>
        </form>
      </div>
    </div>
  );
}

export default SignIn;
