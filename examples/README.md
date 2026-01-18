# Azure Pipelines Examples

This directory contains example Azure Pipelines YAML files demonstrating various parameter configurations.

## all-parameter-types.yml

This example demonstrates **all supported Azure Pipelines parameter types for interactive runtime input** with complete coverage of:

### Parameter Types Covered

1. **String** - Text values
   - With default value
   - Without default value
   - With allowed values (enumeration)

2. **StringList** - Multiple selection from allowed values
   - With default value (subset of allowed values)
   - Without default value (empty selection)
   - **Note**: StringList parameters always require a `values` field with allowed options. Defaults must be a subset of these values.

3. **Number** - Numeric values
   - With default value
   - Without default value
   - With allowed values (enumeration)

4. **Boolean** - True/false values
   - With default value
   - Without default value

5. **Object** - Complex objects with nested properties
   - With default value
   - Without default value

### Purpose

This example serves as:
- **Reference documentation** for all supported interactive parameter types
- **Test data** for validating the PipelineMonitor parameter parsing functionality
- **Template** for developers creating parameterized Azure Pipelines

### Total Parameters

The file contains **12 parameters** covering the 5 interactive parameter types (String, StringList, Number, Boolean, Object):
- Parameters with default values
- Parameters without default values
- Additional variations (e.g., with allowed values)

### Note on Non-Interactive Parameter Types

The following parameter types are **not included** as they are not designed for interactive runtime input:
- Step / StepList
- Job / JobList
- Deployment / DeploymentList
- Stage / StageList

These types are used for template composition and pipeline structure, not for user-provided runtime parameters.
