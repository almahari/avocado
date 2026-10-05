| Goal | Command | Short explanation |
|---|---|---|
| Total number of threads | `grep -c '^"' thread.txt` | Counts thread header lines |
| Count threads per state | `grep 'java.lang.Thread.State:' thread.txt \| awk '{print $2}' \| sort \| uniq -c \| sort -nr` | Shows RUNNABLE, WAITING, BLOCKED, etc. |
| Count RUNNABLE threads | `grep -c 'java.lang.Thread.State: RUNNABLE' thread.txt` | Total RUNNABLE threads |
| Count BLOCKED threads | `grep -c 'java.lang.Thread.State: BLOCKED' thread.txt` | Total BLOCKED threads |
| Count WAITING threads | `grep -c 'java.lang.Thread.State: WAITING' thread.txt` | Total WAITING threads |
| Count TIMED_WAITING threads | `grep -c 'java.lang.Thread.State: TIMED_WAITING' thread.txt` | Total TIMED_WAITING threads |
| Show BLOCKED threads | `grep -B2 -A15 'java.lang.Thread.State: BLOCKED' thread.txt` | Shows blocked thread plus nearby stack trace |
| Show thread name + state | `awk '/^"/ {t=$0} /java.lang.Thread.State:/ {print $2, t}' thread.txt` | Links each state to its thread name |
| Extract only thread names | `sed -n 's/^"\([^"]*\)".*/\1/p' thread.txt` | Prints thread names only |
| Count exact thread names | `sed -n 's/^"\([^"]*\)".*/\1/p' thread.txt \| sort \| uniq -c \| sort -nr` | Finds duplicate/common thread names |
| Count thread types | `sed -n 's/^"\([^"]*\)".*/\1/p' thread.txt \| sed -E 's/-[0-9]+$//' \| sort \| uniq -c \| sort -nr` | Groups names such as `worker-1`, `worker-2` |
| Top 20 thread types | `sed -n 's/^"\([^"]*\)".*/\1/p' thread.txt \| sed -E 's/-[0-9]+$//' \| sort \| uniq -c \| sort -nr \| head -20` | Finds thread pools with the most threads |
| Count JmsConsumer threads | `grep -c '^".*JmsConsumer' thread.txt` | Total JMS consumer threads |
| Show JmsConsumer threads | `grep '^".*JmsConsumer' thread.txt` | Lists JMS consumer thread headers |
| Extract JmsConsumer names | `sed -n '/^".*JmsConsumer/s/^"\([^"]*\)".*/\1/p' thread.txt` | Prints only JMS consumer names |
| Count JMS destinations | `sed -n '/^".*JmsConsumer/s/^"\([^"]*\)".*/\1/p' thread.txt \| sed -E 's/-[0-9]+$//' \| sort \| uniq -c \| sort -nr` | Groups JMS consumers by destination/name pattern |
| Top CPU threads | `awk '/^"/ && match($0,/cpu=([0-9.]+)ms/,a) {print a[1],$0}' thread.txt \| sort -nr \| head -20` | Shows highest accumulated CPU time |
| Top thread elapsed time | `awk '/^"/ && match($0,/elapsed=([0-9.]+)s/,a) {print a[1],$0}' thread.txt \| sort -nr \| head -20` | Shows longest-lived threads |
| Most common stack frames | `sed -n 's/^[[:space:]]*at \(.*\)/\1/p' thread.txt \| sort \| uniq -c \| sort -nr \| head -30` | Finds where most threads are spending time |
| Most common methods | `sed -n 's/^[[:space:]]*at \([^(]*\).*/\1/p' thread.txt \| sort \| uniq -c \| sort -nr \| head -30` | Groups stack traces by method |
| Most common classes | `sed -n 's/^[[:space:]]*at \([^(]*\).*/\1/p' thread.txt \| sed -E 's/\.[^.]+$//' \| sort \| uniq -c \| sort -nr \| head -30` | Groups stack frames by class |
| Top first stack frame | `awk '/^"/ {first=1} first && /^[[:space:]]+at / {print; first=0}' thread.txt \| sed -n 's/^[[:space:]]*at //p' \| sort \| uniq -c \| sort -nr \| head -30` | Shows what threads are primarily waiting/running in |
| Find deadlocks | `grep -i -A30 'deadlock' thread.txt` | Searches JVM deadlock report |
| Find threads waiting for locks | `grep -B5 -A10 'waiting to lock' thread.txt` | Shows lock contention |
| Find owned monitors | `grep -B5 -A5 'locked <' thread.txt` | Shows locks currently owned |
| Find specific lock ID | `grep -n -B5 -A10 '0x0000000123456789' thread.txt` | Finds threads using the same monitor |
| Find socket/network reads | `grep -B3 -A8 -E 'SocketInputStream\|socketRead\|NioSocketImpl.*read' thread.txt` | Finds threads waiting on network I/O |
| Find DB-related threads | `grep -B3 -A10 -Ei 'jdbc\|hibernate\|mybatis\|sqlserver\|mysql\|oracle' thread.txt` | Finds database activity |
| Find JMS-related stacks | `grep -B3 -A10 -Ei 'JmsConsumer\|javax.jms\|jakarta.jms\|solace\|tibco' thread.txt` | Finds messaging-related activity |
| Find executor/pool threads | `grep '^"' thread.txt \| grep -Ei 'pool-\|executor\|ForkJoin\|worker'` | Shows common executor threads |
| Find application threads | `grep -B3 -A10 'com.yourcompany' thread.txt` | Finds threads executing your code |
| Compare two dumps | `diff -u thread1.txt thread2.txt` | Shows what changed between snapshots |
| Threads RUNNABLE in both dumps | `comm -12 <(awk '/^"/{t=$0}/State: RUNNABLE/{print t}' thread1.txt \| sort) <(awk '/^"/{t=$0}/State: RUNNABLE/{print t}' thread2.txt \| sort)` | Helps find continuously active threads |