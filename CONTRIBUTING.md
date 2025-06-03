# Contributing
Contributions are very welcome and greatly appreciated! Make sure to join the [Celeste Community Discord](discord.gg/Celeste) for any help you need.

## Table of Contents
- [Contributing](#contributing)
  - [Table of Contents](#table-of-contents)
- [Getting started](#getting-started)
- [Types of Contributions](#types-of-contributions)
  - [Bug Reports](#bug-reports)
  - [Ideas or Feature Requests](#ideas-or-feature-requests)
  - [Pull Requests](#pull-requests)
- [Making Changes](#making-changes)
  - [Adding Content](#adding-content)
  - [Patching](#patching)
    - [`patch_` classes](#patch_-classes)
    - [`Ext` classes](#ext-classes)
    - [MonoModRules](#monomodrules)
  - [Miscellaneous](#miscellaneous)
    - [Warnings](#warnings)
    - [Documentation](#documentation)

# Getting started
<!--*If you would like to contribute to translating Everest, see [TRANSLATING]().*-->

:warning: Make sure to search the existing [Issues](https://github.com/EverestAPI/Everest/issues?q=is%3Aissue) and [Pull Requests](https://github.com/EverestAPI/Everest/issues?q=is%3Apr) to see if one already exists for your contribution.

If you are planning on contributing code to Everest, it is recommended to create a [fork](https://guides.github.com/activities/forking/) of the main repo to develop on.
Once you have a fork, clone it to your machine and follow the instructions [here](https://github.com/EverestAPI/Everest#compiling-everest-yourself) to build and install it.


# Types of Contributions
These are guidelines for some of the different types of contributions you can make to Everest.

## Bug Reports
Make sure bug reports have enough information to be reproduced easily!  
That means:
- **Steps to reproduce.**
- Description of the bug (expected behaviour vs actual behaviour).
- `log.txt` file from the session where the bug was encountered (located in your Celeste install folder, with previous sessions stored in the `LogHistory` subfolder).

## Ideas or Feature Requests
When submitting ideas or feature requests, consider carefully whether the feature would be more suited for a code mod.  
Features for Everest should match at least one of the following criteria:
- Adds features for code mods.
- Specific to the Everest project.
- Not possible to achieve through a code mod.

## Pull Requests
When making a pull request, please note the following guidelines:
- **Branch name:** use a descriptive name for your branch, following the format of `action-target-details` where possible.
- **Description:** explain what your pull request changes and _why_.
- **Credits:** if your modifications are in response to a request or issue, link to it or otherwise provide a "paper trail".
- **Testing:** if you are submitting a bugfix, describe the issue thouroughly and provide recreation steps, *especially* if it is not an existing [issue](https://github.com/EverestAPI/Everest/issues).
- **Response:** when you submit a pull request, you are responsible for making sure any questions and change requests on it are answered in a timely manner.

Once everything is ready, for it to get merged it will need to get reviewed by at least **2 members** of the Everest team. Once that has happened, there is a **3 day** last-call window in which any concerns can be brought up before it is merged. If nothing is blocking the pull request, it'll get merged once the 3 day window is over.  

If a pull request would be merged within 3 days of a planned release, it will have to be merged **after** it, to ensure it has enough time to be tested before being released. The date of the next release can be found [in the Milestones section](https://github.com/EverestAPI/Everest/milestones).

# Making Changes
These are explanations and guidelines for how to make changes to the Everest source code.

## Adding Content
Content added *must not* change the functionality of the base game, or change any public facing API.  
While many mods also make use of non-public code it is not as important to maintain backwards compatibility for it.

New features should only be added to improve or un-hardcode vanilla features, especially for general use in code mods (f.e. [StrawberryRegistry](https://github.com/EverestAPI/Everest/blob/dev/Celeste.Mod.mm/Mod/Registry/StrawberryRegistry.cs), [CustomNPC](https://github.com/EverestAPI/Everest/blob/dev/Celeste.Mod.mm/Mod/Entities/CustomNPC.cs), [Custom Events](https://github.com/EverestAPI/Everest/blob/dev/Celeste.Mod.mm/Patches/EventTrigger.cs)).  
Gameplay mechanics, entities, and other features not present in the base game are usually better suited for a mod, although some exceptions will be made on a case by case basis.

## Patching
As with in adding content, patches *must not* change the functionality of the base game, or change any public API.

Everest uses [MonoMod](https://github.com/MonoMod/MonoMod) to patch Celeste, which allows for classes and members to be modified.
For an explanation of how the MonoMod patcher works, see the [MonoMod GitHub](https://github.com/MonoMod/MonoMod#faq)

<!-- For an in-depth explanation of applying patches, along with best practices and common pitfalls, see the [Patching Guide](). -->

There are two ways to modify Vanilla code, depending on the scope of what is being modified:

### `patch_` classes
Adding a class with the same name as an existing one, and prefixing it with `patch_` will direct MonoMod to apply it as a patch onto the original class.

By default, any methods that are patched are preserved by MonoMod with an `orig_` prefix, and can be referenced in the patched method:
```cs
public extern void orig_DoThing(int val);
public void DoThing(int val) {
    int newVal = Transform(val);
    orig_DoThing(newVal);
}
```

There are a few attributes that can be applied to members of the class with different effects:
- `[MonoModIgnore]` ignore this member, do not patch it except for MonoMod custom attributes.
- `[MonoModConstructor]` treat this method as a constructor - [why this is needed](https://github.com/MonoMod/MonoMod/issues/51#issuecomment-496115932). 
- `[MonoModReplace]` replace this method entirely, do not generate an `orig_` method.

### `Ext` classes
Code mods were previously created with Everest as a git submodule, which meant that any additions from Everest in `patch_` classes were not available to those mods at build time.

The recommended practice has since been updated to build against a patched version of Celeste, making `Ext` classes relatively obsolete.

In most cases, **new `patch_` members should not be added to their associated `Ext` class, and new `Ext` classes should not be created.**

Exceptions are made when the `Ext` class contains significant additions that are not within the scope of the original class (ex: `TextMenuExt`).

Existing `Ext` classes will also be kept in the following cases:
- An associated `patch_` class member was not made public (ex: `patch_Audio.CheckFmod`)
- Useful extension methods are defined (ex: `AreaDataExt.ToKey`)

### MonoModRules
:information_source: **The recommended practices for MonoModRules have recently been changed as described in [this PR](https://github.com/EverestAPI/Everest/pull/351).**

Everest uses MonoModRules to directly modify the IL code of vanilla methods.  
Some guidelines for using them are as follows:

- Patches and Attribute definitions should be located in the `Celeste.Mod.mm/Patches/` folder alongside their associated [`patch_`](#patch_-classes) class.
  - If a patch is used across multiple files, it can be moved into the main [MonoModRules](https://github.com/EverestAPI/Everest/blob/dev/Celeste.Mod.mm/MonoModRules.cs) file.

- When using types or methods in an IL patch, they must be imported as a reference through `MonoModRule.Modder` or by association with an already imported type.

- While in code mods it is often preferred to fail safe when patching, if an Everest patch does not work it should fail hard to prevent broken builds from reaching end-users.  
This means using `ILCursor.GotoNext` instead of `TryGotoNext` where possible, and implementing additional checks when `TryGotoNext` is necessary.

- Primitive arrays and switch statements with more than 6 cases are not usable due to compiler optimizations.

## Miscellaneous

### Warnings
Due to the nature of modding, there are situations where the C# compiler will generate a warning that isn't possible to fix. In this situation you can add the following preprocessor directive to the top of the file:
```
#pragma warning disable [Warning Number] // Add a comment with the warning description
```
Warnings can be disabled for a specific block of code by surrounding it with `pragma warning disable` and `pragma warning enable`.

### Documentation

Documentation takes the form of comments, xmldoc, guides, and references.

- **Comments** should be added as needed to code, to help other developers and maintainers understand what it does and why it is necessary.
- **XMLDoc** (in-line code docs that show up in IDEs) are recommended to help modders out.
Vanilla types and members should be documented in `lib-stripped/Celeste.xml`.
When adding xmldoc to modded or patched members, the `inheritdoc` tag should be used for members that would otherwise override vanilla docs, and the `origdoc` tag can be added to `orig_` members to warn users against using them.
- **Guides and References** are kept in a number of locations, the most prominent being the [Everest Wiki](https://github.com/EverestAPI/Resources/wiki).
Anything new for use in mods should be included there, with bonus points for adding an example in the [Reference Mod](https://github.com/EverestAPI/ExampleMod), if appropriate.
