using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

class Coroutine {
    public static IEnumerator Do(Action action) {
        action();
        yield return null;
    }

    public static IEnumerator DoAfterSeconds(float seconds, Action action) {
        yield return new WaitForSeconds(seconds);
        action();
    }

    public static IEnumerator DoAtEndOfFrame(Action action) {
        yield return new WaitForEndOfFrame();
        action();
    }

    public static IEnumerator DoDuringFixedUpdate(Action action) {
        yield return new WaitForFixedUpdate();
        action();
    }
}
