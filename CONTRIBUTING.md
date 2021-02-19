# Contributing to the Serilog.Sinks.SyslogMessages project

It's great that you are considering contributing to the Serilog.Sinks.SyslogMessages project :tada:.  As can be seen from the list of [other Serilog sinks][other_sinks] and [community projects][community_projects], it is people like yourself that make the Serilog ecosystem great!

The following are a set of guidelines for contributing to the [Serilog.Sinks.SyslogMessages][this] sink.

## Asking a Questions
We use [GitHub discussions][discussions] for questions and discussions.

## Reporting an issue
Bugs and features/enhancements are tracked via [GitHub issues][issues].  Below are some notes to help create an issue.

### Feature Requests
* If your feature request relates to a problem, please provide a clear and concise description of what the problem is
* Describe the solution you'd like, providing a clear and concise description of what you want to happen
* Provide a clear and concise description of any alternative solutions, workarounds or features you've considered
* Provide any additional context you feel may be helpful, such as code snippets, screenshots or links

### Bug/Issue Reports
* Ensure the bug was not already reported by searching the [issues list][create_issue] - please feel free to comment on any existing bugs if you have the same problem, or would like to help with a solution
* Create an issue via the [issues list][create_issue]
* State the version of Serilog.Sinks.SyslogMessages that is affected
* State the target framework and operating system that is affected
* Provide steps to reproduce the issue
* If possible, provide a minimal sample that reproduces the issue

### Documentation Changes
* If your proposed changes/fixes are quite simple, please feel free to open a PR instead of creating an issue
* Create an issue via the [issues list][create_issue]
* Provide a clear and concise description of the proposed changes, or what you feel is missing

## Making a PR (Pull Request)
Code and documentation changes are handled using [Github Pull Requests](prs). Below are some notes to help create a PR.

* Before creating a PR for a new feature, or for any documentation changes or bug fixes that are **not very simple**, please open an issue via the [issues list][create_issue] - we want to discuss things first before you spend time making a contribution
* Fork the repository and create a branch with a descriptive name
  * We prefer to use branch names in the form `feat/my-feature` or `fix/my-fix`, but it's not a big deal :smile:
* Try to follow the existing coding conventions - the [.editorconfig](https://github.com/IonxSolutions/serilog-sinks-syslog/blob/master/.editorconfig) should provide hints to your preferred IDE
* Attempt to make commits of logical units
* Please include Unit Tests for any new features, and also for bugs if you think a future regression is possible
* When committing, please reference the issue the commit relates to
* Run the build and tests
    * Windows platforms can use the `build.cmd` script
    * Linux platforms can use the `build.sh` script
* Create the PR, including:
    * The issue this PR addresses
    * Unit Tests for the changes have been added

[this]: https://github.com/IonxSolutions/serilog-sinks-syslog
[serilog]: https://github.com/serilog/serilog
[other_sinks]: https://github.com/serilog/serilog/wiki/Provided-Sinks
[community_projects]: https://github.com/serilog/serilog/wiki/Community-Projects
[create_issue]: https://github.com/IonxSolutions/serilog-sinks-syslog/issues/new
[issues]: https://github.com/IonxSolutions/serilog-sinks-syslog/issues
[prs]: https://github.com/IonxSolutions/serilog-sinks-syslog/pulls
[discussions]: https://github.com/IonxSolutions/serilog-sinks-syslog/discussions
