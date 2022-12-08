# Discord Game SDK code

This directory contains the C# code distributed in [version 3.2.1 of the Discord Game SDK](https://dl-game-sdk.discordapp.net/3.2.1/discord_game_sdk.zip).

The SDK was altered to fix a garbage collection issue that causes segfaults on Windows, by changing all the callbacks that way:
```diff
+       private static FFIMethods.ValidateOrExitCallback validateOrExitCallback = ValidateOrExitCallbackImpl;
        [MonoPInvokeCallback]
        private static void ValidateOrExitCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            ValidateOrExitHandler callback = (ValidateOrExitHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void ValidateOrExit(ValidateOrExitHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
-           Methods.ValidateOrExit(MethodsPtr, GCHandle.ToIntPtr(wrapped), ValidateOrExitCallbackImpl);
+           Methods.ValidateOrExit(MethodsPtr, GCHandle.ToIntPtr(wrapped), validateOrExitCallback);
        }
```
