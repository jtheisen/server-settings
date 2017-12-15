Server Settings
===============

This tiny library provides settings to an application from configurable sources, usually either the classical app settings or a config file living at a specific location relative to the current user's home.

It is necessary as it's not possible to make the classical app settings feed on files outside the source tree, let alone on information from programatic sources. This leads to the annoying problem that app settings get checked in to version control even though one often want to have them configured in a developer-specific way during development.
