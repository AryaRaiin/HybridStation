
# sections explained
## top level
### `---`
splits file into "documents"

### `file` (required)
required field, the file to act upon for the whole section

### `template` (optional)
using template file, run document rules for each entry, substitute using ${}

## `match`
each array element is 'or'
if array element multi-value is 'and'
### tags
!Missing = field/value must be missing to match

## `action`

### merge
only merge if match occurs

###  mergeadd add
merge existing, else add new
creates new elements

### remove
remove lowest level elements in data from matches

## `data`
### tags !MATH
perform math against existing value. usually only useful if the match is specific enough to only run once

