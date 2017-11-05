# RepoTools for .NET

RepoTools is a set of command line tools that can be used to create and maintain repositories of file-based data which are easy to validate against a file/hash manifest using modern cryptographic hash methods.  It serves a similar purpose to the Parchive utility, but uses concepts familiar from git.  A RepoTools repository may be used "live", and the tools can be used to update and maintain the repository, as well to compare and synchronize different copies of the repository.  RepoTools is implemented with .NET, but the binaries are compatible with and can be used with the Mono framework.  These tools have not been tested on Microsoft's ports of .NET to other platforms

## Getting Started

To get started, build the tools using Visual Studio.  The project and solution files are compatible with VS 2010, but may be upgraded to later versions of VS.  Once the tools are built, put them into your PATH.  There is a PDF file with an extensive overview, detailed instructions for and examples of use under the Documentation directory.  I'm planning to add a GitHub Wiki in the near future.

### Prerequisites

You'll need Visual Studio 2010 or later to build the project files.  You'll need the Mono framework to execute the commands on platforms other than Windows.  These tools have not been tested on Microsoft's ports of .NET to other platforms, but I would hope that they would work reliably.

## Authors

* **Daniel Oberlin** - *Initial work* - [Daniel-Oberlin](https://github.com/Daniel-Oberlin)

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

* Inspired by git, but serving a similar function to the Parchive utility.
