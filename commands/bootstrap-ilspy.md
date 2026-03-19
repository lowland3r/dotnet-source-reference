# bootstrap-ilspy

Check whether ilspycmd is installed and install it if not.

## Steps

1. Run the following command and capture its output:
   ```
   dotnet tool list -g
   ```

2. **If `dotnet` is not found on PATH:**
   Hard stop. Output exactly:
   ```
   ERROR: dotnet SDK is required to use this plugin.
   Install the .NET SDK from https://dot.net, then re-run /bootstrap-ilspy.
   ```

3. **If output contains a line with `ilspycmd`:**
   Output: "✓ ilspycmd is already installed."
   Done — no further action needed.

4. **If output does not contain `ilspycmd`:**
   Run:
   ```
   dotnet tool install -g ilspycmd
   ```
   Capture all output (stdout + stderr).

5. **If the install command exits with code 0:**
   Output: "✓ ilspycmd installed successfully."

6. **If the install command exits with a non-zero code:**
   Hard stop. Output:
   ```
   ERROR: Failed to install ilspycmd. dotnet tool install output:
   <paste full captured output here verbatim>

   Resolve the error above and re-run /bootstrap-ilspy.
   ```
