#include <ApplicationServices/ApplicationServices.h>
#include <ctype.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
    CGKeyCode key;
    CGEventFlags flags;
    bool has_key;
} Hotkey;

static bool equals_ignore_case(const char *a, const char *b)
{
    while (*a && *b) {
        if (tolower((unsigned char)*a) != tolower((unsigned char)*b))
            return false;
        a++;
        b++;
    }
    return *a == '\0' && *b == '\0';
}

static bool parse_key(const char *token, CGKeyCode *key)
{
    if (equals_ignore_case(token, "Left")) { *key = 123; return true; }
    if (equals_ignore_case(token, "Right")) { *key = 124; return true; }
    if (equals_ignore_case(token, "Down")) { *key = 125; return true; }
    if (equals_ignore_case(token, "Up")) { *key = 126; return true; }
    if (equals_ignore_case(token, "Space")) { *key = 49; return true; }
    if (equals_ignore_case(token, "Tab")) { *key = 48; return true; }
    if (equals_ignore_case(token, "Escape") || equals_ignore_case(token, "Esc")) { *key = 53; return true; }
    if (equals_ignore_case(token, "Return") || equals_ignore_case(token, "Enter")) { *key = 36; return true; }
    if (equals_ignore_case(token, "LeftBracket")) { *key = 33; return true; }
    if (equals_ignore_case(token, "RightBracket")) { *key = 30; return true; }

    if (strlen(token) == 1) {
        switch (tolower((unsigned char)token[0])) {
            case 'a': *key = 0; return true;
            case 's': *key = 1; return true;
            case 'd': *key = 2; return true;
            case 'f': *key = 3; return true;
            case 'h': *key = 4; return true;
            case 'g': *key = 5; return true;
            case 'z': *key = 6; return true;
            case 'x': *key = 7; return true;
            case 'c': *key = 8; return true;
            case 'v': *key = 9; return true;
            case 'b': *key = 11; return true;
            case 'q': *key = 12; return true;
            case 'w': *key = 13; return true;
            case 'e': *key = 14; return true;
            case 'r': *key = 15; return true;
            case 'y': *key = 16; return true;
            case 't': *key = 17; return true;
            case '1': *key = 18; return true;
            case '2': *key = 19; return true;
            case '3': *key = 20; return true;
            case '4': *key = 21; return true;
            case '6': *key = 22; return true;
            case '5': *key = 23; return true;
            case '=': *key = 24; return true;
            case '9': *key = 25; return true;
            case '7': *key = 26; return true;
            case '-': *key = 27; return true;
            case '8': *key = 28; return true;
            case '0': *key = 29; return true;
            case 'o': *key = 31; return true;
            case 'u': *key = 32; return true;
            case 'i': *key = 34; return true;
            case 'p': *key = 35; return true;
            case 'l': *key = 37; return true;
            case 'j': *key = 38; return true;
            case 'k': *key = 40; return true;
            case 'n': *key = 45; return true;
            case 'm': *key = 46; return true;
        }
    }

    return false;
}

static bool parse_token(char *token, Hotkey *hotkey)
{
    if (equals_ignore_case(token, "LeftControl") || equals_ignore_case(token, "RightControl") ||
        equals_ignore_case(token, "Control") || equals_ignore_case(token, "Ctrl")) {
        hotkey->flags |= kCGEventFlagMaskControl;
        return true;
    }

    if (equals_ignore_case(token, "LeftShift") || equals_ignore_case(token, "RightShift") ||
        equals_ignore_case(token, "Shift")) {
        hotkey->flags |= kCGEventFlagMaskShift;
        return true;
    }

    if (equals_ignore_case(token, "LeftAlt") || equals_ignore_case(token, "RightAlt") ||
        equals_ignore_case(token, "Alt") || equals_ignore_case(token, "Option")) {
        hotkey->flags |= kCGEventFlagMaskAlternate;
        return true;
    }

    if (equals_ignore_case(token, "LeftMeta") || equals_ignore_case(token, "RightMeta") ||
        equals_ignore_case(token, "Meta") || equals_ignore_case(token, "Command") || equals_ignore_case(token, "Cmd")) {
        hotkey->flags |= kCGEventFlagMaskCommand;
        return true;
    }

    CGKeyCode key = 0;
    if (parse_key(token, &key)) {
        hotkey->key = key;
        hotkey->has_key = true;
        return true;
    }

    return false;
}

static Hotkey parse_hotkey(const char *text)
{
    Hotkey hotkey = {0, 0, false};
    char *copy = strdup(text);
    if (copy == NULL)
        return hotkey;

    char *save = NULL;
    char *token = strtok_r(copy, "+,; ", &save);
    while (token != NULL) {
        parse_token(token, &hotkey);
        token = strtok_r(NULL, "+,; ", &save);
    }

    free(copy);
    return hotkey;
}

static int post_hotkey(const char *hotkey_text, bool down)
{
    Hotkey hotkey = parse_hotkey(hotkey_text);
    if (!hotkey.has_key)
        return 2;

    CGEventSourceRef source = CGEventSourceCreate(kCGEventSourceStateHIDSystemState);
    CGEventRef event = CGEventCreateKeyboardEvent(source, hotkey.key, down);
    if (event == NULL) {
        if (source != NULL) CFRelease(source);
        return 3;
    }

    CGEventSetFlags(event, hotkey.flags);
    CGEventPost(kCGHIDEventTap, event);

    CFRelease(event);
    if (source != NULL) CFRelease(source);
    return 0;
}

int main(int argc, char **argv)
{
    const char *hotkey = argc > 1 ? argv[1] : "LeftControl+Right";
    const char *action = argc > 2 ? argv[2] : "tap";

    if (equals_ignore_case(action, "down"))
        return post_hotkey(hotkey, true);

    if (equals_ignore_case(action, "up"))
        return post_hotkey(hotkey, false);

    int down = post_hotkey(hotkey, true);
    int up = post_hotkey(hotkey, false);
    return down != 0 ? down : up;
}
