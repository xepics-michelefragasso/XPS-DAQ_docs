

module fifo_mst_write (

  // inputs
  input		RESETN,
  input 	CLK,
  input		RXF_N,
  input 	TXE_N,
  
  
  input                RXF_N, 
  input                TXE_N,
  output               WR_N,    
  output               SIWU_N,
  output               RD_N,
  output               OE_N,
  output [1:0]          BE,
  output [15:0]	       DATA

);


  reg  [6:0]   cur_st;
  reg  [6:0]   nxt_st;
 
  wire         adv_c_rptr;
  wire         load_crptr;
  wire         set_siwu_en;

  reg [8:0] counter;
  reg cout;
  reg tc_txe_n_d1;

  assign BE=2'b11;
  assign OE_N=1'b1;
  assign RD_N=1'b1;
  assign SIWU_N=1'b1;

  
  always @(posedge CLK or negedge RESETN)
    begin
    	if (~RESETN) DATA<=16'h0000;
    	else DATA<= DATA +1;
    end


   
  parameter [2:0] st0=0,st1=1,st2=2,st3=3,st4=4;

  always @(*) begin

    nxt_st      = cur_st;

    case(cur_st)

      st0: begin // initial state

        nxt_st = st1;

      end
      st1: begin


        if (~TXE_N) begin

            nxt_st = st2;
          else
            nxt_st = st1;

        end

      end
      st2: begin
	if (counter==511)
        	nxt_st = st3;
	else:
		nxt_st = st2;

      end
      st3: begin 

	nxt_st=st4
        
      end
      st4: begin // wait until TXN_N go high then going to initial state
	if (TXE_N):
        	nxt_st = st0;
	else:
		nxt_st = st4

      end

     default: begin

        nxt_st = st0;

     end

    endcase

  end

  always @(posedge CLK or negedge RESETN)
    begin	  
    	if (~RESETN) cur_st <=st0;
    	else cur_st<=nxt_st
    end

  always @(cur_st)
    begin:
      case(cur_st)
	st0:WR_N=1;
	st1:WR_N=1;
	st2:WR_N=0;
	st3:WR_N=1;
	st4:WR_N=1;
	default: WR_N=1;
      endcase
   end

	  

  always @(posedge CLK or negedge RESETN)
    begin 
   	 if (~RESETN) begin
	    counter <=0;
   	 end
   	 else if (WR_N) counter <=0;	
	      else counter <= counter +1;
    end


endmodule
