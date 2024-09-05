#!/usr/bin/env python3

import os

print("hello from python!")
print(f"data: {os.environment.get("DISPATCH_DATA")}")
