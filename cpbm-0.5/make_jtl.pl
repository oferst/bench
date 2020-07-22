#!/usr/bin/perl -w
#
# Copyright (c) 2012 Michael Tautschnig <michael.tautschnig@cs.ox.ac.uk>
# Department of Computer Science, Oxford University
# 
# All rights reserved. Redistribution and use in source and binary forms, with
# or without modification, are permitted provided that the following
# conditions are met:
# 
#   1. Redistributions of source code must retain the above copyright
#      notice, this list of conditions and the following disclaimer.
# 
#   2. Redistributions in binary form must reproduce the above copyright
#      notice, this list of conditions and the following disclaimer in the
#      documentation and/or other materials provided with the distribution.
# 
#   3. All advertising materials mentioning features or use of this software
#      must display the following acknowledgement:
# 
#      This product includes software developed by Daniel Kroening,
#      Edmund Clarke, Computer Systems Institute, ETH Zurich
#      Computer Science Department, Carnegie Mellon University
# 
#   4. Neither the name of the University nor the names of its contributors
#      may be used to endorse or promote products derived from this software
#      without specific prior written permission.
# 
#    
# THIS SOFTWARE IS PROVIDED BY THE REGENTS AND CONTRIBUTORS `AS IS'' AND ANY
# EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
# DISCLAIMED.  IN NO EVENT SHALL THE REGENTS OR CONTRIBUTORS BE LIABLE FOR ANY
# DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
# (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
# LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
# ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
# (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
# THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


# build a JMeter XML report

use strict;
use warnings FATAL => qw(uninitialized);
use Date::Parse;

sub usage {
  print <<"EOF";
Usage: $0 CSV
  where CSV is a comma-separated data file as built by make_csv.pl to build a
  JMeter XML file

EOF
}

if (scalar(@ARGV) != 1) {
  usage;
  exit 1;
}

my $file = $ARGV[0];
shift @ARGV;
open my $CSV, "<$file" or die "File $file not found\n";

print <<'EOF';
<?xml version="1.0" encoding="UTF-8"?>
<testResults version="1.2">

EOF

my %globals = ();

use Text::CSV;
my $csv = Text::CSV->new();
my $arref = $csv->getline($CSV);
defined($arref) or die "Failed to parse headers\n";
$csv->column_names(@$arref);

my %col_map = (
  "Benchmark" => "label",
  "Result" => "rm",
  "exitcode" => "rc",
  "date" => "ts",
  "usertime" => "t"
);

while (my $row = $csv->getline_hr($CSV)) {
  foreach (qw(command timeout uname cpuinfo meminfo memlimit)) {
    defined($row->{$_}) or die "No $_ data in table\n";
    defined($globals{$_}) or $globals{$_} = ();
    $globals{$_}{$row->{$_}} = 1;
  }

  print "<sample";
  foreach my $c (keys %col_map) {
    print " ".$col_map{$c}."=\"";
    defined($row->{$c}) or die "No $c data in table\n";
    my $val = $row->{$c};
    if($c eq "date")
    {
      my $time = str2time($val);
      print $time*1000;
    }
    elsif($c eq "usertime")
    {
      print $val*1000;
    }
    else
    {
      print $val;
    }
    print "\"";
  }
  print "/>\n";
}

print "\n</testResults>\n";

close $CSV;

