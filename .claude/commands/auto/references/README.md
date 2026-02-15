# Auto Workflow References

Bundled resources for the auto workflow skill.

## Structure

```
auto/
├── references/
│   ├── README.md                  <- This file
│   ├── phases.md                  <- Detailed phase instructions (1-5)
│   └── risk-classification.md     <- Risk levels and pre-flight checks
└── (future: scripts/, assets/)
```

## Usage

Files in this folder are loaded by Claude only when needed during workflow execution.
The main `auto.md` file provides an overview and links to these references.
